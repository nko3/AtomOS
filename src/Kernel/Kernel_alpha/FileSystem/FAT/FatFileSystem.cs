﻿using System;
using System.Collections.Generic;
using Kernel_alpha.Drivers;
using Kernel_alpha.FileSystem.FAT;
using Kernel_alpha.Lib.Encoding;
using Kernel_alpha.FileSystem.FAT.Lists;

namespace Kernel_alpha.FileSystem
{
    public class FatFileSystem : GenericFileSystem
    {
        private UInt32 BytePerSector;
        private UInt32 SectorsPerCluster;
        private UInt32 ReservedSector;
        private UInt32 TotalFAT;
        private UInt32 DirectoryEntry;
        private UInt32 TotalSectors;
        private UInt32 SectorsPerFAT;
        private UInt32 DataSectorCount;
        private UInt32 ClusterCount;
        private FatType FatType;
        private UInt32 SerialNo;
        private UInt32 RootCluster;
        private UInt32 RootSector;
        private UInt32 RootSectorCount;
        private UInt32 DataSector;
        private UInt32 EntriesPerSector;
        private UInt32 fatEntries;
        private string VolumeLabel;
                
        public FatFileSystem(BlockDevice aDevice)
        {
            this.IDevice = aDevice;
            this.mIsValid = IsFAT();
        }

        private bool IsFAT()
        {
            var BootSector = new byte[512];
            this.IDevice.Read(0U, 1U, BootSector);

            var xSig = BitConverter.ToUInt16(BootSector, 510);
            if (xSig != 0xAA55)
                return false;

            /* BPB (BIOS Parameter Block) */
            BytePerSector = BitConverter.ToUInt16(BootSector, 11);
            SectorsPerCluster = BootSector[13];
            ReservedSector = BitConverter.ToUInt16(BootSector, 14);
            TotalFAT = BootSector[16];
            DirectoryEntry = BitConverter.ToUInt16(BootSector, 17);

            if (BitConverter.ToUInt16(BootSector, 19) == 0)
            {
                /* Large amount of sector on media. This field is set if there are more than 65535 sectors in the volume. */
                TotalSectors = BitConverter.ToUInt32(BootSector, 32);
            }
            else
            {
                TotalSectors = BitConverter.ToUInt16(BootSector, 19);
            }

            /* FAT 12 and FAT 16 ONLY */
            SectorsPerFAT = BitConverter.ToUInt16(BootSector, 22);

            if (SectorsPerFAT == 0)
            {
                /* FAT 32 ONLY */
                SectorsPerFAT = BitConverter.ToUInt32(BootSector, 36);
            }

            /* Not Necessary, To Avoid Crashes during corrupted BPB Info */
            //Just to prevent ourself from hacking
            if (TotalFAT == 0 || TotalFAT > 2 || BytePerSector == 0 || TotalSectors == 0)
                return false;

            /* Some basic calculations to check basic error :P */
            try
            {
                uint RootDirSectors = 0;
                DataSectorCount = TotalSectors - (ReservedSector + (TotalFAT * SectorsPerFAT) + RootDirSectors);
                ClusterCount = DataSectorCount / SectorsPerCluster;
            }
            catch
            {
                return false;
            }

            /* Finally we got key xD */
            if (ClusterCount < 4085)
                FatType = FatType.FAT12;
            else if (ClusterCount < 65525)
                FatType = FatType.FAT16;
            else
                FatType = FatType.FAT32;

            /* Now we open door of gold coins xDD */
            if (FatType == FatType.FAT32)
            {
                SerialNo = BitConverter.ToUInt32(BootSector, 39);
                VolumeLabel = ASCII.GetString(BootSector, 71, 11);   // for checking              
                RootCluster = BitConverter.ToUInt32(BootSector, 44);
                RootSector = 0;
                RootSectorCount = 0;
            }
            /* The key is of another door */
            else
            {
                SerialNo = BitConverter.ToUInt32(BootSector, 67);
                VolumeLabel = ASCII.GetString(BootSector, 43, 11);
                RootSector = ReservedSector + (TotalFAT * SectorsPerFAT);
                RootSectorCount = (UInt32)((DirectoryEntry * 32 + (BytePerSector - 1)) / BytePerSector);
                fatEntries = SectorsPerFAT * 512 / 4;
            }
            /* Now it shows our forward path ;) */
            EntriesPerSector = (UInt32)(BytePerSector / 32);
            DataSector = ReservedSector + (TotalFAT * SectorsPerFAT) + RootSectorCount;
            return true;
        }

        public RootDirectory ReadDirectory(UInt32 Cluster)
        {
            UInt32 xSector = DataSector + ((Cluster - 2) * SectorsPerCluster);
            var xResult = new RootDirectory(this, xSector);

            byte[] aData = new byte[(UInt32)(512 * SectorsPerCluster)];
            this.IDevice.Read(xSector, SectorsPerCluster, aData);

            #region ReadingCode
            uint Entry_offset = 0;
            bool Entry_Type; //True -> Directory & False -> File
            string Entry_Name;
            string Entry_Ext;
            Details Entry_Detail = new Details();//We init it once, because we love memory =P
            for (uint i = 0; i < SectorsPerCluster * 512; i+= 32)
            {
                if (aData[i] == 0x0)
                    break;
                else
                {
                    //Find Entry Type
                    switch(aData[i + 11])
                    {
                        case 0x10:
                            Entry_Type = true;
                            break;
                        case 0x20:
                            Entry_Type = false;
                            break;
                        default:
                            continue;
                    }

                    Entry_offset = i;

                    if (aData[i] != 0xE5)//Entry Exist
                    {
                        Entry_Name = ASCII.GetString(aData, (int)i, 8).Trim();

                        /*
                         * plz leave this one me =D
                        Entry_Detail.Attribute = 0;
                        Entry_Detail.CrtDate = 0;
                        Entry_Detail.CrtTime = 0;
                        Entry_Detail.FileSize = BitConverter.ToUInt32(aData, (int)(i + Entry.FileSize));
                        Entry_Detail.StartCluster = 0;
                        Entry_Detail.WrtDate = 0;
                        Entry_Detail.WrtTime = 0;
                        */
                        if (!Entry_Type)
                        {
                            Entry_Ext = ASCII.GetString(aData, (int)(i + 8), 3).Trim();
                            xResult.AddEntry(new File(Entry_Name + "." + Entry_Ext, Entry_Detail));
                        }
                        else
                        {
                            xResult.AddEntry(new Directory(Entry_Name, Entry_Detail));
                        }
                    }
                }
            }
            #endregion

            return xResult;
        }

        public void FlushDetails()
        {
            if (IsValid)
            {
                Console.WriteLine("FAT Version:" + ((FatType == FatType.FAT32) ? "FAT32" : "FAT16/12"));                
                //Console.WriteLine("Disk Volume:" + (VolumeLabel == "NO NAME" ? VolumeLabel + "<Extended>" : VolumeLabel));
                Console.WriteLine("Bytes Per Sector:" + BytePerSector.ToString());
                Console.WriteLine("Sectors Per Cluster:" + SectorsPerCluster.ToString());
                Console.WriteLine("Reserved Sector:" + ReservedSector.ToString());
                Console.WriteLine("Total FAT:" + TotalFAT.ToString());
                Console.WriteLine("Direactory Entry:" + DirectoryEntry.ToString());
                Console.WriteLine("Total Sectors:" + TotalSectors.ToString());
                Console.WriteLine("Sectors Per FAT:" + SectorsPerFAT.ToString());
                Console.WriteLine("Data Sector Count:" + DataSectorCount.ToString());
                Console.WriteLine("Cluster Count:" + ClusterCount.ToString());
                Console.WriteLine("Serial Number:" + SerialNo.ToString());
                Console.WriteLine("Root Cluster:" + RootCluster.ToString());
                Console.WriteLine("Root Sector:" + RootSector.ToString());
                Console.WriteLine("Root Sector Count:" + RootSectorCount.ToString());
                Console.WriteLine("Data Sector:" + DataSector.ToString());
            }
            else
                Console.WriteLine("No fat available");
        }
    }
}