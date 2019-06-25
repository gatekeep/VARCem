/*
 * VARCem	Virtual ARchaeological Computer EMulator.
 *		An emulator of (mostly) x86-based PC systems and devices,
 *		using the ISA,EISA,VLB,MCA  and PCI system buses, roughly
 *		spanning the era between 1981 and 1995.
 *
 *		This file is part of the VARCem Project.
 *
 *		Handle UDP-socket library processing.
 *
 * Version:	@(#)net_udp.c	1.0.0	2018/09/19
 *
 * Author:	Bryan Biedenkapp, <gatekeep@gmail.com>
 *
 *		Copyright 2018 Bryan Biedenkapp
 *
 *		Redistribution and  use  in source  and binary forms, with
 *		or  without modification, are permitted  provided that the
 *		following conditions are met:
 *
 *		1. Redistributions of  source  code must retain the entire
 *		   above notice, this list of conditions and the following
 *		   disclaimer.
 *
 *		2. Redistributions in binary form must reproduce the above
 *		   copyright  notice,  this list  of  conditions  and  the
 *		   following disclaimer in  the documentation and/or other
 *		   materials provided with the distribution.
 *
 *		3. Neither the  name of the copyright holder nor the names
 *		   of  its  contributors may be used to endorse or promote
 *		   products  derived from  this  software without specific
 *		   prior written permission.
 *
 * THIS SOFTWARE  IS  PROVIDED BY THE  COPYRIGHT  HOLDERS AND CONTRIBUTORS
 * "AS IS" AND  ANY EXPRESS  OR  IMPLIED  WARRANTIES,  INCLUDING, BUT  NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
 * PARTICULAR PURPOSE  ARE  DISCLAIMED. IN  NO  EVENT  SHALL THE COPYRIGHT
 * HOLDER OR  CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL,  EXEMPLARY,  OR  CONSEQUENTIAL  DAMAGES  (INCLUDING,  BUT  NOT
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE  GOODS OR SERVICES;  LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED  AND ON  ANY
 * THEORY OF  LIABILITY, WHETHER IN  CONTRACT, STRICT  LIABILITY, OR  TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING  IN ANY  WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#include <stdio.h>
#include <stdint.h>
#include <string.h>
#include <stdlib.h>
#include <wchar.h>
#if defined(_WIN32) && !defined(WIN32)
#define WIN32
#endif
#include "udp_socket.h"
#include "../../emu.h"
#include "../../config.h"
#include "../../device.h"
#include "../../plat.h"
#include "../../ui/ui.h"
#include "../../zlib/zlib.h"
#include "network.h"

# define UDP_DLL_PATH	"libudp"

static volatile thread_t	*poll_tid;
static event_t				*poll_state;

static int					is_server_connected = FALSE;
static int					pkt_poller_running = TRUE;

static struct in_addr		srv_addr;
static unsigned int			srv_port;
static uint8_t*             netcard_mac;

#define RX_BUF_SIZE			65535

#define PKT_MAGIC			0x4958
#define CS_CMD_REG			0xFF
#define CS_CMD_UNREG		0xFA

struct handshake_hdr {
	uint16_t magic;
	uint8_t checksum;
	uint16_t data_len;
	uint16_t compress_len;
	uint8_t mac_addr[6];
	uint16_t length;
};

#define CONVIP(hostvar) hostvar & 0xff, (hostvar >> 8) & 0xff, (hostvar >> 16) & 0xff, (hostvar >> 24) & 0xff
#define CONVMAC(hostvar) hostvar[0], hostvar[1], hostvar[2], hostvar[3], hostvar[4], hostvar[5]


static uint8_t
packet_crc(uint8_t *buffer, uint16_t bufSize)
{
	uint8_t tmpCRC = 0;
	uint16_t i;
	for (i = 0; i < bufSize; i++) {
		tmpCRC ^= *buffer;
		buffer++;
	}
	return tmpCRC;
}

static void
udp_disconnect_from_server(int unexpected, uint8_t *mac)
{
	if (unexpected)
		INFO("UDP: server disconnected unexpectedly\n");
	if (is_server_connected) {
		struct handshake_hdr *hdr = (struct handshake_hdr*)malloc(sizeof(struct handshake_hdr));
		hdr->magic = PKT_MAGIC;

		hdr->checksum = CS_CMD_UNREG;
		hdr->data_len = 0;
		hdr->compress_len = 0;

		hdr->mac_addr[0] = &mac[0];
		hdr->mac_addr[1] = &mac[1];
		hdr->mac_addr[2] = &mac[2];
		hdr->mac_addr[3] = &mac[3];
		hdr->mac_addr[4] = &mac[4];
		hdr->mac_addr[5] = &mac[5];

		hdr->length = sizeof(struct handshake_hdr);

		udp_socket_write((uint8_t *)hdr, hdr->length, srv_addr, srv_port);
		is_server_connected = FALSE;

		// send shutdown string to server
		udp_socket_write((uint8_t *)hdr, hdr->length, srv_addr, srv_port);
	}
}

static int
udp_connect_to_server(char const *addr, uint16_t port, uint8_t *mac)
{
	srv_addr = udp_socket_lookup(addr);
	if (srv_addr.s_addr != INADDR_NONE) {
		struct handshake_hdr *hdr = (struct handshake_hdr*)malloc(sizeof(struct handshake_hdr));
		hdr->magic = PKT_MAGIC;

		hdr->checksum = CS_CMD_REG;
		hdr->data_len = 0;
		hdr->compress_len = 0;

		hdr->mac_addr[0] = &mac[0];
		hdr->mac_addr[1] = &mac[1];
		hdr->mac_addr[2] = &mac[2];
		hdr->mac_addr[3] = &mac[3];
		hdr->mac_addr[4] = &mac[4];
		hdr->mac_addr[5] = &mac[5];

		hdr->length = sizeof(struct handshake_hdr);

		int ret = udp_socket_write((uint8_t *)hdr, hdr->length, srv_addr, srv_port);
		if (!ret) {
			INFO("UDP: unable to connect to server: %s\n", addr);
			free(hdr);
			return FALSE;
		}

		free(hdr);
		return TRUE;
	}
	else
        INFO("UDP: Unable resolve connection to server %s", addr);
	return FALSE;
}


/* Handle the receiving of frames from the channel. */
static void
poll_thread(void *arg)
{
	uint8_t *mac = (uint8_t *)arg;
	uint8_t *data = NULL;
    uint32_t mac_cmp32[2];
    uint16_t mac_cmp16[2];

	struct in_addr src_addr;
	uint32_t src_port;

	event_t *evt;

    INFO("UDP: polling started.\n");
	thread_set_event(poll_state);

	/* Create a waitable event. */
	evt = thread_create_event();

    data = (uint8_t*)malloc(RX_BUF_SIZE);

    /* As long as the channel is open.. */
	while (pkt_poller_running) {
		/* Request ownership of the device. */
		network_wait(1);

		/* Wait for a poll request. */
		network_poll();

        /* Wait for the next packet to arrive. */
		memset(data, 0, RX_BUF_SIZE);

		int len = udp_socket_read(data, RX_BUF_SIZE, &src_addr, &src_port);
        if (len > 0) {
            /* create and copy header */
            struct handshake_hdr* hdr = (struct handshake_hdr*)malloc(sizeof(struct handshake_hdr));
            memcpy(hdr, data, sizeof(struct handshake_hdr));

            if (hdr->magic == PKT_MAGIC) {
                if (len < hdr->length)
                    continue;

                switch (hdr->checksum) {
                case CS_CMD_REG:
                {
                    INFO("UDP: connected to server\n");
                    is_server_connected = TRUE;
                }
                break;

                default:
                {
                    /* only process requests if the is_server_connected flag is TRUE */
                    if (is_server_connected) {
                        if ((hdr->data_len > 0) && (hdr->compress_len > 0)) {
                            uint16_t data_len = hdr->data_len;
                            uint16_t compressed_size = hdr->compress_len;

                            /* copy compressed data */
                            uint8_t* compressed_data = (uint8_t*)malloc(compressed_size);
                            memcpy(compressed_data, data + sizeof(struct handshake_hdr), compressed_size);

                            /* decompress data */
                            uint8_t* rawData = (uint8_t*)malloc(data_len);
                            unsigned long decomp_len = data_len;
                            uncompress(rawData, &decomp_len, compressed_data, compressed_size);
                            free(compressed_data);

                            /* check data checksum */
                            uint8_t checksum = packet_crc(&rawData[0], data_len);
                            if (hdr->checksum != checksum)
                                continue;

                            /* Received MAC. */
                            mac_cmp32[0] = *(uint32_t*)(data + 6);
                            mac_cmp16[0] = *(uint16_t*)(data + 10);

                            /* Local MAC. */
                            mac_cmp32[1] = *(uint32_t*)mac;
                            mac_cmp16[1] = *(uint16_t*)(mac + 4);
                            if ((mac_cmp32[0] != mac_cmp32[1]) ||
                                (mac_cmp16[0] != mac_cmp16[1])) {
                                network_rx(rawData, data_len);
                            }

                            free(rawData);
                        }
                    }
                }
                break;
                }
            }

            free(hdr);
        }
        else {
            if (!is_server_connected) {
                udp_connect_to_server(config.network_srv_addr, config.network_srv_port, netcard_mac);
            }
        }

		/* If we did not get anything, wait a while. */
		if (len == 0)
			thread_wait_event(evt, 10);

		/* Release ownership of the device. */
		network_wait(0);
	}

    free(data);

	if (is_server_connected) {
		udp_disconnect_from_server(0, netcard_mac);
	}

	/* No longer needed. */
	if (evt != NULL)
		thread_destroy_event(evt);

    INFO("UDP: polling stopped.\n");
	thread_set_event(poll_state);
}


/*
 * Initialize UDP-socket for use.
 *
 * This is called only once, during application init,
 * so the UI can be properly initialized.
 */
static int
do_init(netdev_t* list)
{
    const char* fn = UDP_DLL_PATH;

    INFO("UDP: initializing\n");

    /* Forward module name back to caller. */
    strcpy(list->description, fn);

	udp_socket_init(0);
    udp_socket_open();

	return(1);
}


/* Close up shop. */
static void
do_close(void)
{
	INFO("UDP: closing.\n");

	/* Tell the thread to terminate. */
	if (poll_tid != NULL) {
		network_busy(0);

		pkt_poller_running = FALSE;

		/* Wait for the thread to finish. */
        INFO("UDP: waiting for thread to end...\n");
		thread_wait_event(poll_state, -1);
        INFO("UDP: thread ended\n");
		thread_destroy_event(poll_state);

		poll_tid = NULL;
		poll_state = NULL;
	}

	/* OK, now shut down UDP itself. */
	udp_socket_close();
}


/*
 * Reset UDP and activate it.
 *
 * This is called on every 'cycle' of the emulator,
 * if and as long the NetworkType is set to UDP,
 * and also as long as we have a NetCard defined.
 */
static int
do_reset(uint8_t *mac)
{
    /* Get the value of our server address. */
    if ((config.network_srv_addr[0] == '\0') || !strcmp(config.network_srv_addr, "none")) {
        ERRLOG("UDP: no UDP server address configured!\n");
        return(-1);
    }

    /* Get the value of our server port. */
    if (config.network_srv_port == 0) {
        ERRLOG("UDP: no UDP server port configured!\n");
        return(-1);
    }

    srv_port = config.network_srv_port;

	/* Tell the thread to terminate. */
	if (poll_tid != NULL) {
		network_busy(0);

		/* Wait for the thread to finish. */
        INFO("UDP: waiting for thread to end...\n");
		thread_wait_event(poll_state, -1);
        INFO("UDP: thread ended\n");
		thread_destroy_event(poll_state);
	}

    /* Make sure local variables are cleared. */
    poll_tid = NULL;
    poll_state = NULL;

	/* OK, now shut down UDP itself. */
	udp_socket_close();
	udp_socket_init(0);
	udp_socket_open();

	/* Save the callback info. */
    INFO("UDP: starting thread..\n");

    netcard_mac = mac;

	poll_state = thread_create_event();
	poll_tid = thread_create(poll_thread, mac);
	thread_wait_event(poll_state, -1);

	return(0);
}


/* Are we available or not? */
static int
do_available(void)
{
    return 1;
}

/* Send a packet to the UDP interface. */
static void
do_send(uint8_t *bufp, int len)
{
	network_busy(1);

	struct handshake_hdr *hdr = (struct handshake_hdr*)malloc(sizeof(struct handshake_hdr));
	unsigned long compressed_size = compressBound(len);
	uint8_t *compressed_packet = (uint8_t *)malloc(compressed_size);

	/* compress packet */
	int zErr;
	if ((zErr = compress(compressed_packet, &compressed_size, bufp, len)) != Z_OK)
        ERRLOG("UDP: failed to compress outgoing packet");

	/* create packet header */
	hdr->magic = PKT_MAGIC;
	hdr->checksum = packet_crc(&bufp[0], len);
	hdr->data_len = len;
	hdr->compress_len = (uint16_t)compressed_size;
	memcpy(hdr->mac_addr, netcard_mac, sizeof(netcard_mac));
	hdr->length = (uint16_t)compressed_size + sizeof(struct handshake_hdr);

	uint8_t *bufo = (uint8_t *)malloc(hdr->length);
	memset(bufo, 0, hdr->length);

	/* add packet header to output data */
	memcpy(bufo, hdr, sizeof(struct handshake_hdr));

	/* copy actual packet data to output */
	memcpy(bufo + sizeof(struct handshake_hdr), compressed_packet, compressed_size);

	int ret = udp_socket_write(bufo, hdr->length, srv_addr, srv_port);
	if (ret == FALSE)
		ERRLOG("UDP: could not send packet");

	free(bufo);
	free(compressed_packet);
	free(hdr);

	network_busy(0);
}


const network_t network_udp = {
    "UDP Tunnel",
    do_init, do_close, do_reset,
    do_available,
    do_send
};
