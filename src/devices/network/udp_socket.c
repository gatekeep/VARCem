/*
 * VARCem	Virtual ARchaeological Computer EMulator.
 *		An emulator of (mostly) x86-based PC systems and devices,
 *		using the ISA,EISA,VLB,MCA  and PCI system buses, roughly
 *		spanning the era between 1981 and 1995.
 *
 *		This file is part of the VARCem Project.
 *
 *		Implementation for the UDP-socket communication.
 *
 * Version:	@(#)udp_socket.h	1.0.0	2018/09/19
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
#include <stdarg.h>
#define HAVE_STDARG_H
#include "udp_socket.h"
#include "../../emu.h"
#include "../../device.h"
#include "network.h"

unsigned int	udp_socket_port;
int				udp_socket_fd;


void
udp_socket_init(uint32_t port)
{
	udp_socket_port = port;
	udp_socket_fd = -1;

#if defined(_WIN32) || defined(_WIN64)
	WSADATA data;
	int wsaRet = WSAStartup(MAKEWORD(2, 2), &data);
	if (wsaRet != 0)
        ERRLOG("UDPSOCKET: error from WSAStartup");
#endif
}


struct in_addr
udp_socket_lookup(const char* hostname)
{
	struct in_addr addr;
#if defined(_WIN32) || defined(_WIN64)
	unsigned long address = inet_addr(hostname);
	if (address != INADDR_NONE && address != INADDR_ANY) {
		addr.s_addr = address;
		return addr;
	}

	struct hostent* hp = gethostbyname(hostname);
	if (hp != NULL) {
		memcpy(&addr, hp->h_addr_list[0], sizeof(struct in_addr));
		return addr;
	}

    ERRLOG("UDPSOCKET: cannot find address for host %s", hostname);

	addr.s_addr = INADDR_NONE;
	return addr;
#else
	struct in_addr_t address = inet_addr(hostname);
	if (address != in_addr_t(-1)) {
		addr.s_addr = address;
		return addr;
	}

	struct hostent* hp = gethostbyname(hostname);
	if (hp != NULL) {
		memcpy(&addr, hp->h_addr_list[0], sizeof(struct in_addr));
		return addr;
	}

    ERRLOG("UDPSOCKET: cannot find address for host %s", hostname);

	addr.s_addr = INADDR_NONE;
	return addr;
#endif
}


int
udp_socket_open()
{
	udp_socket_fd = socket(PF_INET, SOCK_DGRAM, 0);
	if (udp_socket_fd < 0) {
#if defined(_WIN32) || defined(_WIN64)
        ERRLOG("UDPSOCKET: cannot create the UDP socket, err: %lu", GetLastError());
#else
        ERRLOG("UDPSOCKET: cannot create the UDP socket, err: %d", errno);
#endif
		return FALSE;
	}

	struct sockaddr_in addr;
	memset(&addr, 0x00, sizeof(struct sockaddr_in));
	addr.sin_family = AF_INET;
	addr.sin_port = htons(udp_socket_port);
	addr.sin_addr.s_addr = htonl(INADDR_ANY);

	int reuse = 1;
	if (setsockopt(udp_socket_fd, SOL_SOCKET, SO_REUSEADDR, (char *)&reuse, sizeof(reuse)) == -1) {
#if defined(_WIN32) || defined(_WIN64)
        ERRLOG("UDPSOCKET: cannot set the UDP socket option, err: %lu", GetLastError());
#else
        ERRLOG("UDPSOCKET: cannot set the UDP socket option, err: %d", errno);
#endif
		return FALSE;
	}

	if (bind(udp_socket_fd, (struct sockaddr*)&addr, sizeof(struct sockaddr_in)) == -1) {
#if defined(_WIN32) || defined(_WIN64)
        ERRLOG("UDPSOCKET: cannot bind the UDP address, err: %lu", GetLastError());
#else
        ERRLOG("UDPSOCKET: cannot bind the UDP address, err: %d", errno);
#endif
		return FALSE;
	}

	return TRUE;
}


int
udp_socket_read(uint8_t* buffer, unsigned int length, struct in_addr* address, uint32_t* port)
{
	if (buffer == NULL)
		return -1;
	if (length <= 0U)
		return -1;

	// Check that the readfrom() won't block
	fd_set readFds;
	FD_ZERO(&readFds);
#if defined(_WIN32) || defined(_WIN64)
	FD_SET((unsigned int)udp_socket_fd, &readFds);
#else
	FD_SET(udp_socket_fd, &readFds);
#endif

	// Return immediately
	struct timeval tv;
	tv.tv_sec = 0L;
	tv.tv_usec = 0L;

	int ret = select(udp_socket_fd + 1, &readFds, NULL, NULL, &tv);
	if (ret < 0) {
#if defined(_WIN32) || defined(_WIN64)
        ERRLOG("UDPSOCKET: error returned from UDP select, err: %lu", GetLastError());
#else
        ERRLOG("UDPSOCKET: error returned from UDP select, err: %d", errno);
#endif
		return -1;
	}

	if (ret == 0)
		return 0;

	struct sockaddr_in addr;
#if defined(_WIN32) || defined(_WIN64)
	int size = sizeof(struct sockaddr_in);
#else
	socklen_t size = sizeof(struct sockaddr_in);
#endif

#if defined(_WIN32) || defined(_WIN64)
	int len = recvfrom(udp_socket_fd, (char*)buffer, length, 0, (struct sockaddr *)&addr, &size);
#else
	ssize_t len = recvfrom(udp_socket_fd, (char*)buffer, length, 0, (struct sockaddr *)&addr, &size);
#endif
	if (len <= 0) {
#if defined(_WIN32) || defined(_WIN64)
        ERRLOG("UDPSOCKET: error returned from recvfrom, err: %lu", GetLastError());
#else
        ERRLOG("UDPSOCKET: error returned from recvfrom, err: %d", errno);
#endif
		return -1;
	}

	*address = addr.sin_addr;
	*port = ntohs(addr.sin_port);

	return len;
}


int
udp_socket_write(const uint8_t* buffer, unsigned int length, const struct in_addr address, uint32_t port)
{
	if (buffer == NULL)
		return FALSE;
	if (length <= 0U)
		return FALSE;

	struct sockaddr_in addr;
	memset(&addr, 0x00, sizeof(struct sockaddr_in));

	addr.sin_family = AF_INET;
	addr.sin_addr = address;
	addr.sin_port = htons(port);

#if defined(_WIN32) || defined(_WIN64)
	int ret = sendto(udp_socket_fd, (char *)buffer, length, 0, (struct sockaddr *)&addr, sizeof(struct sockaddr_in));
#else
	ssize_t ret = sendto(udp_socket_fd, (char *)buffer, length, 0, (struct sockaddr *)&addr, sizeof(struct sockaddr_in));
#endif
	if (ret < 0) {
#if defined(_WIN32) || defined(_WIN64)
        ERRLOG("UDPSOCKET: error returned from sendto, err: %lu", GetLastError());
#else
        ERRLOG("UDPSOCKET: error returned from sendto, err: %d", errno);
#endif
		return FALSE;
	}

#if defined(_WIN32) || defined(_WIN64)
	if (ret != length)
		return FALSE;
#else
	if (ret != ssize_t(length))
		return FALSE;
#endif

	return TRUE;
}


void
udp_socket_close()
{
#if defined(_WIN32) || defined(_WIN64)
	closesocket(udp_socket_fd);
	WSACleanup();
#else
	close(udp_socket_fd);
#endif
}
