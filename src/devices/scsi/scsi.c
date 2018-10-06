/*
 * VARCem	Virtual ARchaeological Computer EMulator.
 *		An emulator of (mostly) x86-based PC systems and devices,
 *		using the ISA,EISA,VLB,MCA  and PCI system buses, roughly
 *		spanning the era between 1981 and 1995.
 *
 *		This file is part of the VARCem Project.
 *
 *		Handling of the SCSI controllers.
 *
 * Version:	@(#)scsi.c	1.0.11	2018/10/05
 *
 * Authors:	Fred N. van Kempen, <decwiz@yahoo.com>
 *		Miran Grca, <mgrca8@gmail.com>
 *		TheCollector1995, <mariogplayer@gmail.com>
 *
 *		Copyright 2017,2018 Fred N. van Kempen.
 *		Copyright 2016-2018 Miran Grca.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free  Software  Foundation; either  version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is  distributed in the hope that it will be useful, but
 * WITHOUT   ANY  WARRANTY;  without  even   the  implied  warranty  of
 * MERCHANTABILITY  or FITNESS  FOR A PARTICULAR  PURPOSE. See  the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the:
 *
 *   Free Software Foundation, Inc.
 *   59 Temple Place - Suite 330
 *   Boston, MA 02111-1307
 *   USA.
 */
#include <stdio.h>
#include <stdint.h>
#include <string.h>
#include <stdlib.h>
#include <stdarg.h>
#include <wchar.h>
#define HAVE_STDARG_H
#define dbglog scsi_log
#include "../../emu.h"
#include "../../device.h"
#include "../../plat.h"
#include "../disk/hdc.h"
#include "../disk/hdd.h"
#include "scsi.h"
#include "scsi_device.h"
#include "scsi_disk.h"
#include "../cdrom/cdrom.h"
#include "../disk/zip.h"
#include "scsi_aha154x.h"
#include "scsi_buslogic.h"
#include "scsi_ncr5380.h"
#include "scsi_ncr53c810.h"
#ifdef USE_WD33C93
# include "scsi_wd33c93.h"
#endif


#ifdef ENABLE_SCSI_LOG
int		scsi_do_log = ENABLE_SCSI_LOG;
#endif
#ifdef ENABLE_SCSI_DEV_LOG
int		scsi_dev_do_log = ENABLE_SCSI_DEV_LOG;
#endif
scsi_device_t	SCSIDevices[SCSI_ID_MAX][SCSI_LUN_MAX];

uint32_t	SCSI_BufferLength;


static struct {
    const char		*internal_name;
    const device_t	*device;
} scsi_cards[] = {
    { "none",		NULL				},

    { "aha1540b",	&aha1540b_device		},
    { "aha1542c",	&aha1542c_device		},
    { "aha1542cf",	&aha1542cf_device		},
    { "bt542bh",	&buslogic_device		},
    { "bt545s",		&buslogic_545s_device		},
    { "lcs6821n",	&scsi_lcs6821n_device		},
    { "rt1000b",	&scsi_rt1000b_device		},
    { "t130b",		&scsi_t130b_device		},
    { "scsiat",		&scsi_scsiat_device		},
#ifdef USE_WD33C93
    { "wd33c93",	&scsi_wd33c93_device		},
#endif

    { "aha1640",	&aha1640_device			},
    { "bt640a",		&buslogic_640a_device		},

    { "bt958d",		&buslogic_pci_device		},
    { "ncr53c810",	&ncr53c810_pci_device		},

    { "bt445s",		&buslogic_445s_device		},

    { NULL,		NULL				}
};


const char *
scsi_card_get_internal_name(int card)
{
    return(scsi_cards[card].internal_name);
}


int
scsi_card_get_from_internal_name(const char *s)
{
    int c = 0;

    while (scsi_cards[c].internal_name != NULL) {
	if (! strcmp(scsi_cards[c].internal_name, s))
		return(c);
	c++;
    }

    /* Not found. */
    return(0);
}


const char *
scsi_card_getname(int card)
{
    if (scsi_cards[card].device != NULL)
	return(scsi_cards[card].device->name);

    return(NULL);
}


const device_t *
scsi_card_getdevice(int card)
{
    return(scsi_cards[card].device);
}


int
scsi_card_has_config(int card)
{
    if (scsi_cards[card].device != NULL)
	return(scsi_cards[card].device->config ? 1 : 0);

    return(0);
}


void
scsi_log(int level, const char *fmt, ...)
{
#ifdef ENABLE_SCSI_LOG
    va_list ap;

    if (scsi_do_log >= level) {
	va_start(ap, fmt);
	pclog_ex(fmt, ap);
	va_end(ap);
    }
#endif
}


void
scsi_dev_log(int level, const char *fmt, ...)
{
#ifdef ENABLE_SCSI_DEV_LOG
    va_list ap;

    if (scsi_dev_do_log >= level) {
	va_start(ap, fmt);
	pclog_ex(fmt, ap);
	va_end(ap);
    }
#endif
}


int
scsi_card_available(int card)
{
    if (scsi_cards[card].device)
	return(device_available(scsi_cards[card].device));

    return(1);
}


void
scsi_card_init(void)
{
    int i, j;

    if (! scsi_cards[scsi_card].device) return;

    DEBUG("SCSI: building hard disk map...\n");
    build_scsi_disk_map();

    DEBUG("SCSI: building CD-ROM map...\n");
    build_scsi_cdrom_map();

    DEBUG("SCSI: building ZIP map...\n");
    zip_build_scsi_map();
	
    for (i = 0; i < SCSI_ID_MAX; i++) {
	for (j = 0; j < SCSI_LUN_MAX; j++) {
		if (scsi_disks[i][j] != 0xff)
			SCSIDevices[i][j].LunType = SCSI_DISK;
		  else if (scsi_cdrom_drives[i][j] != 0xff)
			SCSIDevices[i][j].LunType = SCSI_CDROM;
		  else if (scsi_zip_drives[i][j] != 0xff)
			SCSIDevices[i][j].LunType = SCSI_ZIP;
		  else
			SCSIDevices[i][j].LunType = SCSI_NONE;

		SCSIDevices[i][j].CmdBuffer = NULL;
	}
    }

    device_add(scsi_cards[scsi_card].device);
}
