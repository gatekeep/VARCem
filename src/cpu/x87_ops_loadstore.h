/*
 * VARCem	Virtual ARchaeological Computer EMulator.
 *		An emulator of (mostly) x86-based PC systems and devices,
 *		using the ISA,EISA,VLB,MCA  and PCI system buses, roughly
 *		spanning the era between 1981 and 1995.
 *
 *		This file is part of the VARCem Project.
 *
 *		x87 FPU instructions core.
 *
 * Version:	@(#)x87_ops_loadstore.h	1.0.2	2018/09/13
 *
 * Authors:	Sarah Walker, <tommowalker@tommowalker.co.uk>
 *		Miran Grca, <mgrca8@gmail.com>
 *
 *		Copyright 2008-2018 Sarah Walker.
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

static int opFILDiw_a16(uint32_t fetchdat)
{
        int16_t temp;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FILDw %08X:%08X\n", easeg, cpu_state.eaaddr);
        temp = geteaw(); if (cpu_state.abrt) return 1;
        if (fplog) DEBUG("  %f\n", (double)temp);
        x87_push((double)temp);
        CLOCK_CYCLES(13);
        return 0;
}
static int opFILDiw_a32(uint32_t fetchdat)
{
        int16_t temp;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FILDw %08X:%08X\n", easeg, cpu_state.eaaddr);
        temp = geteaw(); if (cpu_state.abrt) return 1;
        if (fplog) DEBUG("  %f\n", (double)temp);
        x87_push((double)temp);
        CLOCK_CYCLES(13);
        return 0;
}

static int opFISTiw_a16(uint32_t fetchdat)
{
        int64_t temp64;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FISTw %08X:%08X\n", easeg, cpu_state.eaaddr);
        temp64 = x87_fround(ST(0));
/*                        if (temp64 > 32767 || temp64 < -32768)
                           fatal("FISTw overflow %i\n", temp64);*/
        seteaw((int16_t)temp64);
        CLOCK_CYCLES(29);
        return cpu_state.abrt;
}
static int opFISTiw_a32(uint32_t fetchdat)
{
        int64_t temp64;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FISTw %08X:%08X\n", easeg, cpu_state.eaaddr);
        temp64 = x87_fround(ST(0));
/*                        if (temp64 > 32767 || temp64 < -32768)
                           fatal("FISTw overflow %i\n", temp64);*/
        seteaw((int16_t)temp64);
        CLOCK_CYCLES(29);
        return cpu_state.abrt;
}

static int opFISTPiw_a16(uint32_t fetchdat)
{
        int64_t temp64;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FISTw %08X:%08X\n", easeg, cpu_state.eaaddr);
        temp64 = x87_fround(ST(0));
/*                        if (temp64 > 32767 || temp64 < -32768)
                           fatal("FISTw overflow %i\n", temp64);*/
        seteaw((int16_t)temp64); if (cpu_state.abrt) return 1;
        x87_pop();
        CLOCK_CYCLES(29);
        return 0;
}
static int opFISTPiw_a32(uint32_t fetchdat)
{
        int64_t temp64;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FISTw %08X:%08X\n", easeg, cpu_state.eaaddr);
        temp64 = x87_fround(ST(0));
/*                        if (temp64 > 32767 || temp64 < -32768)
                           fatal("FISTw overflow %i\n", temp64);*/
        seteaw((int16_t)temp64); if (cpu_state.abrt) return 1;
        x87_pop();
        CLOCK_CYCLES(29);
        return 0;
}

static int opFILDiq_a16(uint32_t fetchdat)
{
        int64_t temp64;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FILDl %08X:%08X\n", easeg, cpu_state.eaaddr);
        temp64 = geteaq(); if (cpu_state.abrt) return 1;
        if (fplog) DEBUG("  %f  %08X %08X\n", (double)temp64, readmeml(easeg,cpu_state.eaaddr), readmeml(easeg,cpu_state.eaaddr+4));
        x87_push((double)temp64);
        cpu_state.MM[cpu_state.TOP].q = temp64;
        cpu_state.tag[cpu_state.TOP] |= TAG_UINT64;

        CLOCK_CYCLES(10);
        return 0;
}
static int opFILDiq_a32(uint32_t fetchdat)
{
        int64_t temp64;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FILDl %08X:%08X\n", easeg, cpu_state.eaaddr);
        temp64 = geteaq(); if (cpu_state.abrt) return 1;
        if (fplog) DEBUG("  %f  %08X %08X\n", (double)temp64, readmeml(easeg,cpu_state.eaaddr), readmeml(easeg,cpu_state.eaaddr+4));
        x87_push((double)temp64);
        cpu_state.MM[cpu_state.TOP].q = temp64;
        cpu_state.tag[cpu_state.TOP] |= TAG_UINT64;

        CLOCK_CYCLES(10);
        return 0;
}

static int FBSTP_a16(uint32_t fetchdat)
{
        double tempd;
        int c;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FBSTP %08X:%08X\n", easeg, cpu_state.eaaddr);
        tempd = ST(0);
        if (tempd < 0.0) 
                tempd = -tempd;
        for (c = 0; c < 9; c++)
        {
                uint8_t tempc = (uint8_t)floor(fmod(tempd, 10.0));
                tempd -= floor(fmod(tempd, 10.0));
                tempd /= 10.0;
                tempc |= ((uint8_t)floor(fmod(tempd, 10.0))) << 4;
                tempd -= floor(fmod(tempd, 10.0));
                tempd /= 10.0;
                writememb(easeg, cpu_state.eaaddr + c, tempc);
        }
        tempc = (uint8_t)floor(fmod(tempd, 10.0));
        if (ST(0) < 0.0) tempc |= 0x80;
        writememb(easeg, cpu_state.eaaddr + 9, tempc); if (cpu_state.abrt) return 1;
        x87_pop();
        return 0;
}
static int FBSTP_a32(uint32_t fetchdat)
{
        double tempd;
        int c;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FBSTP %08X:%08X\n", easeg, cpu_state.eaaddr);
        tempd = ST(0);
        if (tempd < 0.0) 
                tempd = -tempd;
        for (c = 0; c < 9; c++)
        {
                uint8_t tempc = (uint8_t)floor(fmod(tempd, 10.0));
                tempd -= floor(fmod(tempd, 10.0));
                tempd /= 10.0;
                tempc |= ((uint8_t)floor(fmod(tempd, 10.0))) << 4;
                tempd -= floor(fmod(tempd, 10.0));
                tempd /= 10.0;
                writememb(easeg, cpu_state.eaaddr + c, tempc);
        }
        tempc = (uint8_t)floor(fmod(tempd, 10.0));
        if (ST(0) < 0.0) tempc |= 0x80;
        writememb(easeg, cpu_state.eaaddr + 9, tempc); if (cpu_state.abrt) return 1;
        x87_pop();
        return 0;
}

static int FISTPiq_a16(uint32_t fetchdat)
{
        int64_t temp64;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FISTPl %08X:%08X\n", easeg, cpu_state.eaaddr);
        if (cpu_state.tag[cpu_state.TOP] & TAG_UINT64)
                temp64 = cpu_state.MM[cpu_state.TOP].q;
        else
                temp64 = x87_fround(ST(0));
        seteaq(temp64); if (cpu_state.abrt) return 1;
        x87_pop();
        CLOCK_CYCLES(29);
        return 0;
}
static int FISTPiq_a32(uint32_t fetchdat)
{
        int64_t temp64;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FISTPl %08X:%08X\n", easeg, cpu_state.eaaddr);
        if (cpu_state.tag[cpu_state.TOP] & TAG_UINT64)
                temp64 = cpu_state.MM[cpu_state.TOP].q;
        else
                temp64 = x87_fround(ST(0));
        seteaq(temp64); if (cpu_state.abrt) return 1;
        x87_pop();
        CLOCK_CYCLES(29);
        return 0;
}

static int opFILDil_a16(uint32_t fetchdat)
{
        int32_t templ;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FILDs %08X:%08X\n", easeg, cpu_state.eaaddr);
        templ = geteal(); if (cpu_state.abrt) return 1;
        if (fplog) DEBUG("  %f %08X %i\n", (double)templ, templ, templ);
        x87_push((double)templ);
        CLOCK_CYCLES(9);
        return 0;
}
static int opFILDil_a32(uint32_t fetchdat)
{
        int32_t templ;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FILDs %08X:%08X\n", easeg, cpu_state.eaaddr);
        templ = geteal(); if (cpu_state.abrt) return 1;
        if (fplog) DEBUG("  %f %08X %i\n", (double)templ, templ, templ);
        x87_push((double)templ);
        CLOCK_CYCLES(9);
        return 0;
}

static int opFISTil_a16(uint32_t fetchdat)
{
        int64_t temp64;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FISTs %08X:%08X\n", easeg, cpu_state.eaaddr);
        temp64 = x87_fround(ST(0));
/*                        if (temp64 > 2147483647 || temp64 < -2147483647)
                           fatal("FISTl out of range! %i\n", temp64);*/
        seteal((int32_t)temp64);
        CLOCK_CYCLES(28);
        return cpu_state.abrt;
}
static int opFISTil_a32(uint32_t fetchdat)
{
        int64_t temp64;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FISTs %08X:%08X\n", easeg, cpu_state.eaaddr);
        temp64 = x87_fround(ST(0));
/*                        if (temp64 > 2147483647 || temp64 < -2147483647)
                           fatal("FISTl out of range! %i\n", temp64);*/
        seteal((int32_t)temp64);
        CLOCK_CYCLES(28);
        return cpu_state.abrt;
}

static int opFISTPil_a16(uint32_t fetchdat)
{
        int64_t temp64;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FISTs %08X:%08X\n", easeg, cpu_state.eaaddr);
        temp64 = x87_fround(ST(0));
/*                        if (temp64 > 2147483647 || temp64 < -2147483647)
                           fatal("FISTl out of range! %i\n", temp64);*/
        seteal((int32_t)temp64); if (cpu_state.abrt) return 1;
        x87_pop();
        CLOCK_CYCLES(28);
        return 0;
}
static int opFISTPil_a32(uint32_t fetchdat)
{
        int64_t temp64;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FISTs %08X:%08X\n", easeg, cpu_state.eaaddr);
        temp64 = x87_fround(ST(0));
/*                        if (temp64 > 2147483647 || temp64 < -2147483647)
                           fatal("FISTl out of range! %i\n", temp64);*/
        seteal((int32_t)temp64); if (cpu_state.abrt) return 1;
        x87_pop();
        CLOCK_CYCLES(28);
        return 0;
}

static int opFLDe_a16(uint32_t fetchdat)
{
        double t;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FLDe %08X:%08X\n", easeg, cpu_state.eaaddr);                        
        t=x87_ld80(); if (cpu_state.abrt) return 1;
        if (fplog) DEBUG("  %f\n", t);
        x87_push(t);
        CLOCK_CYCLES(6);
        return 0;
}
static int opFLDe_a32(uint32_t fetchdat)
{
        double t;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FLDe %08X:%08X\n", easeg, cpu_state.eaaddr);                        
        t=x87_ld80(); if (cpu_state.abrt) return 1;
        if (fplog) DEBUG("  %f\n", t);
        x87_push(t);
        CLOCK_CYCLES(6);
        return 0;
}

static int opFSTPe_a16(uint32_t fetchdat)
{
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FSTPe %08X:%08X\n", easeg, cpu_state.eaaddr);
        x87_st80(ST(0)); if (cpu_state.abrt) return 1;
        x87_pop();
        CLOCK_CYCLES(6);
        return 0;
}
static int opFSTPe_a32(uint32_t fetchdat)
{
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FSTPe %08X:%08X\n", easeg, cpu_state.eaaddr);
        x87_st80(ST(0)); if (cpu_state.abrt) return 1;
        x87_pop();
        CLOCK_CYCLES(6);
        return 0;
}

static int opFLDd_a16(uint32_t fetchdat)
{
        x87_td t;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FLDd %08X:%08X\n", easeg, cpu_state.eaaddr);
        t.i = geteaq(); if (cpu_state.abrt) return 1;
        if (fplog) DEBUG("  %f\n", t.d);                        
        x87_push(t.d);
        CLOCK_CYCLES(3);
        return 0;
}
static int opFLDd_a32(uint32_t fetchdat)
{
        x87_td t;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FLDd %08X:%08X\n", easeg, cpu_state.eaaddr);
        t.i = geteaq(); if (cpu_state.abrt) return 1;
        if (fplog) DEBUG("  %f\n", t.d);                        
        x87_push(t.d);
        CLOCK_CYCLES(3);
        return 0;
}

static int opFSTd_a16(uint32_t fetchdat)
{
        x87_td t;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FSTd %08X:%08X\n", easeg, cpu_state.eaaddr);
        t.d = ST(0);
        seteaq(t.i);
        CLOCK_CYCLES(8);
        return cpu_state.abrt;
}
static int opFSTd_a32(uint32_t fetchdat)
{
        x87_td t;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FSTd %08X:%08X\n", easeg, cpu_state.eaaddr);
        t.d = ST(0);
        seteaq(t.i);
        CLOCK_CYCLES(8);
        return cpu_state.abrt;
}

static int opFSTPd_a16(uint32_t fetchdat)
{
        x87_td t;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        CHECK_WRITE(cpu_state.ea_seg, cpu_state.eaaddr, cpu_state.eaaddr + 7);
        if (fplog) DEBUG("FSTd %08X:%08X\n", easeg, cpu_state.eaaddr);
        t.d = ST(0);
        seteaq(t.i); if (cpu_state.abrt) return 1;
        x87_pop();
        CLOCK_CYCLES(8);
        return 0;
}
static int opFSTPd_a32(uint32_t fetchdat)
{
        x87_td t;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        CHECK_WRITE(cpu_state.ea_seg, cpu_state.eaaddr, cpu_state.eaaddr + 7);
        if (fplog) DEBUG("FSTd %08X:%08X\n", easeg, cpu_state.eaaddr);
        t.d = ST(0);
        seteaq(t.i); if (cpu_state.abrt) return 1;
        x87_pop();
        CLOCK_CYCLES(8);
        return 0;
}

static int opFLDs_a16(uint32_t fetchdat)
{
        x87_ts ts;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FLDs %08X:%08X\n", easeg, cpu_state.eaaddr);                        
        ts.i = geteal(); if (cpu_state.abrt) return 1;
        if (fplog) DEBUG("  %f\n", ts.s);
        x87_push((double)ts.s);
        CLOCK_CYCLES(3);
        return 0;
}
static int opFLDs_a32(uint32_t fetchdat)
{
        x87_ts ts;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FLDs %08X:%08X\n", easeg, cpu_state.eaaddr);                        
        ts.i = geteal(); if (cpu_state.abrt) return 1;
        if (fplog) DEBUG("  %f\n", ts.s);
        x87_push((double)ts.s);
        CLOCK_CYCLES(3);
        return 0;
}

static int opFSTs_a16(uint32_t fetchdat)
{
        x87_ts ts;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FSTs %08X:%08X\n", easeg, cpu_state.eaaddr);
        ts.s = (float)ST(0);
        seteal(ts.i);
        CLOCK_CYCLES(7);
        return cpu_state.abrt;
}
static int opFSTs_a32(uint32_t fetchdat)
{
        x87_ts ts;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FSTs %08X:%08X\n", easeg, cpu_state.eaaddr);
        ts.s = (float)ST(0);
        seteal(ts.i);
        CLOCK_CYCLES(7);
        return cpu_state.abrt;
}

static int opFSTPs_a16(uint32_t fetchdat)
{
        x87_ts ts;
        FP_ENTER();
        fetch_ea_16(fetchdat);
        if (fplog) DEBUG("FSTs %08X:%08X\n", easeg, cpu_state.eaaddr);
        ts.s = (float)ST(0);
        seteal(ts.i); if (cpu_state.abrt) return 1;
        x87_pop();
        CLOCK_CYCLES(7);
        return 0;
}
static int opFSTPs_a32(uint32_t fetchdat)
{
        x87_ts ts;
        FP_ENTER();
        fetch_ea_32(fetchdat);
        if (fplog) DEBUG("FSTs %08X:%08X\n", easeg, cpu_state.eaaddr);
        ts.s = (float)ST(0);
        seteal(ts.i); if (cpu_state.abrt) return 1;
        x87_pop();
        CLOCK_CYCLES(7);
        return 0;
}
