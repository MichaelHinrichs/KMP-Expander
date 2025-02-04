﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace KMPExpander
{
    
    public partial class KMPEd : Form
    {
        const string Ver = "v1.6";
        KMP keiempi;
        KMP bin;
        int SizeOfKMP;
        readonly byte[] PointSizes = new byte[18] { 28, 24, 72, 20, 28, 24, 16, 64, 255, 48, 255, 28, 255, 255, 255, 255, 24, 22 }; //Size of points inside the sections. If set to 255, the program will ignore anything related to it, including the CSV operations.
        readonly byte[] NumData = new byte[18] { 7, 9, 35, 5, 14, 12, 15, 23, 4, 18, 255, 8, 255, 255, 255, 255, 6, 16 }; //Number of fields on each section
        readonly string[,] SecMagic = new string[2, 18] {
                                                {"TPTK","TPNE","HPNE","TPTI","HPTI","TPKC","HPKC","JBOG","ITOP","AERA","EMAC","TPGJ","TPNC","TPSM","IGTS","SROC","TPLG","HPLG"},
                                                {"KTPT (Kart Point)","ENPT (Enemy Routes)","ENPH (Enemy Routes' Sections)","ITPT (Item Routes)","ITPH (Item Routes' Sections)","CKPT (Checkpoints)","CKPH (Checkpoints' Sections)","GOBJ (Global Objects)","POTI (Routes)","AREA","CAME (Camera)","JGPT (Respawn Points)","CNPT (Cannon Points)","MSPT (Mission Points)","STGI","CORS","GLPT (Glider Points)","GLPH (Glider Points' Sections)"}
                                              };
        readonly int[] Offsets= new int[18];
        string csv = ""; // Comma Separated Values
        string[] csv_parse;
        const string csv_intro = "#Exported from KMP Expander (" + Ver + ") - made by Ermelber\n";


        string GroupBy4(int pos)
        {
            char[] TempStr = new char[4];
            TempStr[0]=(char)keiempi[pos];
            TempStr[1]=(char)keiempi[pos+1];
            TempStr[2]=(char)keiempi[pos+2];
            TempStr[3]=(char)keiempi[pos+3];
            string s = new string(TempStr);
            return s;
        }

        string GroupBy4Bin(int pos)
        {
            char[] TempStr = new char[4];
            TempStr[0] = (char)bin[pos];
            TempStr[1] = (char)bin[pos + 1];
            TempStr[2] = (char)bin[pos + 2];
            TempStr[3] = (char)bin[pos + 3];
            string s = new string(TempStr);
            return s;
        }

        void GetSections()
        {
            int ofs = 0;
            for (int pos = 0; (pos < SizeOfKMP) && (ofs < 18); pos++)
                if (SecMagic[0, ofs] == GroupBy4(pos))
                {
                    Offsets[ofs] = pos - 0x58; //Subtract the header size to the section offset
                    ofs++;
                }
        }

        # region FILE CHECKS
        void EnableEditing()
        {
            comboBox1.Enabled = true;
            saveAsToolStripMenuItem.Enabled = true;
            label2.Visible = true;
            filesize_box.Text = SizeOfKMP.ToString() + " bytes";
        }

        bool CheckSignature()
        {
            bool Check;
            Check = (GroupBy4(0) == "DMDC") && GroupBy4(88) == SecMagic[0, 0];
            return Check;
        }

        bool CheckNumSections(ref int num)
        {
            bool Check;
            for (int pos = 0; (pos < SizeOfKMP - 4) && num < 18; pos++)
                if (SecMagic[0, num] == GroupBy4(pos)) num++;
            Check = num == 18;
            return Check;
        }

        bool CheckFileSize()
        {
            bool Check;
            int filesize;
            filesize = keiempi[4] | keiempi[4 + 1] << 8;
            Check = filesize != SizeOfKMP;
            return Check;
        }

        void UpdateFileSize()
        {
            keiempi[4] = (byte)(SizeOfKMP & 0xFF);
            keiempi[5] = (byte)((SizeOfKMP >> 8) & 0xFF);
        }

        bool CheckSections()
        {
            bool Check = false;
            byte pos = 0x10;
            GetSections();
            for (int i = 0; (i < 18) && Check == false; i++, pos += 4)
                Check = (keiempi[pos] | keiempi [pos + 1] << 8) != Offsets[i];
            return Check;
        }

        void UpdateSections()
        {
            GetSections();
            byte pos = 0x10;
            for (int i = pos; i < 0x58; i++)
                keiempi[i] = 0;
            for (int i = 0; i < 18 ; i++, pos += 4)
            {
                keiempi[pos] = (byte)(Offsets[i] & 0xFF);
                keiempi[pos + 1] = (byte)((Offsets[i] >> 8) & 0xFF);
            }
        }

        void CheckHeader()
        {
            int num = 0;
            if (CheckSignature())
            {
                if (CheckNumSections(ref num))
                {
                    if (CheckFileSize())
                        MessageBox.Show("Internal Filesize isn't correct. It will be adjusted now.");
                    if (CheckSections())
                        MessageBox.Show("There seems to be some inconstistencies between the header and sections. It will be corrected now.");
                    UpdateFileSize();
                    UpdateSections();
                }
                else MessageBox.Show(SecMagic[1, num] + " Section seems to be missing!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else MessageBox.Show("This KMP's Header doesn't look like a MK7 one!\nCheck if the Signature is correct (It should be DMDC) or if the Header's size is 0x58", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        bool CheckBinaryMagic(byte index)
        {
            return GroupBy4Bin(0) == SecMagic[0, index];
        }

        bool CheckNumPoints(byte index,ref short numpt)
        {
            int size = bin.Data.Length;
            numpt = (short)((size - 8) / PointSizes[index]);
            return (size - 8) % PointSizes[index] == 0;
        }

        void CheckSectPoint()
        {
            bool tf = true;
            int size;
            short numpt;
            for (int i = 0;(i < 18) && tf; i++)
            {
                if (PointSizes[i] != 255)
                {
                    if (i == 17)
                        size = SizeOfKMP - Offsets[i] + 0x58;
                    else size = Offsets[i + 1] - Offsets[i];
                    numpt = (short)((size - 8) / PointSizes[i]);
                    tf = ((size - 8) % PointSizes[i]) == 0;
                    if (tf)
                    {
                        if ((keiempi[Offsets[i] + 0x58 + 4] | keiempi[Offsets[i] + 0x58 + 5] << 8) != numpt)
                        {
                            MessageBox.Show(SecMagic[1, i] + "'s amount of points is incorrect. It'll be corrected automatically");
                            keiempi[Offsets[i] + 0x58 + 4] = (byte)(numpt & 0xFF);
                            keiempi[Offsets[i] + 0x58 + 5] = (byte)(numpt >> 8 & 0xFF);
                        }
                    }
                    else MessageBox.Show(SecMagic[1, i] + " seems to be damaged! KMP won't be loaded.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            if (tf) EnableEditing();
        }
        #endregion

        #region File Operations
        /// <summary>
        /// Returns true is c is a hexadecimal digit (A-F, a-f, 0-9)
        /// </summary>
        /// <param name="c">Character to test</param>
        /// <returns>true if hex digit, false if not</returns>
        private static bool IsHexDigit(char c)
        {
            int numChar;
            int numA = Convert.ToInt32('A');
            int num1 = Convert.ToInt32('0');
            c = char.ToUpper(c);
            numChar = Convert.ToInt32(c);
            if (numChar >= numA && numChar < numA + 6)
                return true;
            if (numChar >= num1 && numChar < num1 + 10)
                return true;
            return false;
        }

        /// <summary>
        /// Converts 1 or 2 character string into equivalant byte value
        /// </summary>
        /// <param name="hex">1 or 2 character string</param>
        /// <returns>byte</returns>

        private static byte HexToByte(string hex)
        {
            if (hex.Length > 2 || hex.Length <= 0)
                throw new ArgumentException("hex must be 1 or 2 characters in length");
            byte newByte = byte.Parse(hex, System.Globalization.NumberStyles.HexNumber);
            return newByte;
        }

        /// <summary>
        /// Creates a byte array from the hexadecimal string. Each two characters are combined
        /// to create one byte. First two hexadecimal characters become first byte in returned array.
        /// Non-hexadecimal characters are ignored. 
        /// </summary>
        /// <param name="hexString">string to convert to byte array</param>
        /// <param name="discarded">number of characters in string ignored</param>
        /// <returns>byte array, in the same left-to-right order as the hexString</returns>
        private static byte[] GetBytes(string hexString, out int discarded)
        {
            discarded = 0;
            string newString = "";
            char c;
            // remove all none A-F, 0-9, characters
            for (int i = 0; i < hexString.Length; i++)
            {
                c = hexString[i];
                if (IsHexDigit(c))
                    newString += c;
                else
                    discarded++;
            }
            // if odd number of characters, discard last character
            if (newString.Length % 2 != 0)
            {
                discarded++;
                newString = newString.Substring(0, newString.Length - 1);
            }
            int byteLength = newString.Length / 2;
            byte[] bytes = new byte[byteLength];
            string hex;
            int j = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                hex = new string(new char[] { newString[j], newString[j + 1] });
                bytes[i] = HexToByte(hex);
                j += 2;
            }
            return bytes;
        }

        public static byte[] StringToByte(string a)
        {
            byte[] buffer;
            buffer = GetBytes(a, out int discarded);
            return buffer;
        }

        float Rad2Deg(float angle)
        {
            return (float)(angle * (180.0 / Math.PI));
        }

        float Deg2Rad(float angle)
        {
            return (float)(Math.PI * angle / 180.0);
        }

        //Gets which Object/CAME etc uses a route.
        void UsedBy(ref short[,] mat)
        {
            //Get Object's routes
            short temp_route;
            short numpt_gobj = (short)(keiempi[Offsets[7] + 0x58 + 4] | keiempi[Offsets[7] + 0x58 + 5] << 8);
            for (int i = 0; i < numpt_gobj; i++)
            {
                temp_route = (short)(keiempi[Offsets[7] + 0x58 + 48 + i * PointSizes[7]] | keiempi[Offsets[7] + 0x58 + 49 + i * PointSizes[7]] << 8);
                if (temp_route > -1)
                {
                    mat[temp_route, 0] = (short)i;
                    mat[temp_route, 1] = 0;//Used by a GOBJ
                }
            }
        }

        void Inject(byte[] data)
        {
            var kmp_lst = new List<byte>();
            kmp_lst.AddRange(keiempi.Data);
            int size;
            if (comboBox1.SelectedIndex == 17)
                size = SizeOfKMP - (Offsets[comboBox1.SelectedIndex] + 0x58);
            else size = Offsets[comboBox1.SelectedIndex + 1] - Offsets[comboBox1.SelectedIndex];
            kmp_lst.RemoveRange(Offsets[comboBox1.SelectedIndex] + 0x58, size);
            kmp_lst.InsertRange(Offsets[comboBox1.SelectedIndex] + 0x58, data);
            keiempi.Data = kmp_lst.ToArray();
            GetSections();
            SizeOfKMP = keiempi.Data.Length;
            UpdateFileSize();
            UpdateSections();
            filesize_box.Text = SizeOfKMP.ToString() + " bytes";
        }

        void ImportBin()
        {
            short numpt = 0;
            if (CheckBinaryMagic((byte)comboBox1.SelectedIndex))
            {
                if (PointSizes[comboBox1.SelectedIndex] != 255)
                {
                    if (CheckNumPoints((byte)comboBox1.SelectedIndex, ref numpt))
                    {
                        if ((bin[4] | bin[5] << 8) != numpt)
                        {
                            MessageBox.Show(SecMagic[1, comboBox1.SelectedIndex] + "'s amount of points is incorrect. It'll be corrected automatically");
                            bin[4] = (byte)(numpt & 0xFF);
                            bin[5] = (byte)((numpt >> 8) & 0xFF);
                        }
                        Inject(bin.Data);
                        MessageBox.Show(SecMagic[1, comboBox1.SelectedIndex] + " injected successfully!");
                    }
                    else MessageBox.Show("This binary file seems damaged.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    //This is for unhandled sections I have still to research.
                    Inject(bin.Data);
                    MessageBox.Show(SecMagic[1, comboBox1.SelectedIndex] + " injected successfully!");
                }
            }
            else MessageBox.Show("This file's Magic doesn't match " + SecMagic[1, comboBox1.SelectedIndex] + "'s one!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        void ExportCSV()
        {
            short numpt;
            short numpoti;//Used only for POTI!
            int Section = comboBox1.SelectedIndex;
            numpt = (short)(keiempi[Offsets[Section] + 0x58 + 4] | keiempi[Offsets[Section] + 0x58 + 5] << 8);
            numpoti = (short)(keiempi[Offsets[Section] + 0x58 + 6] | keiempi[Offsets[Section] + 0x58 + 7] << 8);
            if (Section != 8)
                csv = csv_intro + "#" + SecMagic[1, Section] + "\n" + "#Amount of Points: " + numpt.ToString() + "\n";
            switch (Section)
            {
                //KTPT
                case 0:
                    csv += "# X,Y,Z,X Angle,Y Angle,Z Angle,Index\n";
                    for (int i = 0; i < numpt; i++)
                    {
                        csv += BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 8 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 12 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 16 + i * PointSizes[Section]) + ","
                             + Rad2Deg(BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 20 + i * PointSizes[Section])) + ","
                             + Rad2Deg(BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 24 + i * PointSizes[Section])) + ","
                             + Rad2Deg(BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 28 + i * PointSizes[Section])) + ","
                             + BitConverter.ToInt32(keiempi.Data, Offsets[Section] + 0x58 + 32 + i * PointSizes[Section]) + "\n";
                    }
                    break;
                //ENPT
                case 1:
                    csv += "#X,Y,Z,Point Size,Mushroom Settings,Drift Settings,Flags,PathFindOption,MaxSearchYOffset\n";
                    for (int i = 0; i < numpt; i++)
                    {
                        csv += BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 8 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 12 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 16 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 20 + i * PointSizes[Section]) + ","
                             + BitConverter.ToUInt16(keiempi.Data, Offsets[Section] + 0x58 + 24 + i * PointSizes[Section]) + ","
                             + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 26 + i * PointSizes[Section], 1) + ","
                             + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 27 + i * PointSizes[Section], 1) + ","
                             + BitConverter.ToUInt16(keiempi.Data, Offsets[Section] + 0x58 + 28 + i * PointSizes[Section]) + ","
                             + BitConverter.ToUInt16(keiempi.Data, Offsets[Section] + 0x58 + 30 + i * PointSizes[Section]) + "\n";
                    }
                    break;
                //ENPH
                case 2:
                    csv += "#Start,Length,Previous1,Previous2,Previous3,Previous4,Previous5,Previous6,Previous7,Previous8,Previous9,Previous10,Previous11,Previous12,Previous13,Previous14,Previous15,Previous16,Next1,Next2,Next3,Next4,Next5,Next6,Next7,Next8,Next9,Next10,Next11,Next12,Next13,Next14,Next15,Next16,Unknown\n";
                    for (int i = 0; i < numpt; i++)
                    {
                        csv += (short)(keiempi[Offsets[Section] + 0x58 + 8 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 8 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 10 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 10 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 12 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 12 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 14 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 14 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 16 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 16 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 18 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 18 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 20 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 20 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 22 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 22 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 24 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 24 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 26 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 26 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 28 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 28 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 30 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 30 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 32 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 32 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 34 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 34 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 36 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 36 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 38 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 38 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 40 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 40 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 42 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 42 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 44 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 44 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 46 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 46 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 48 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 48 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 50 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 50 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 52 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 52 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 54 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 54 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 56 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 56 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 58 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 58 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 60 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 60 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 62 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 62 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 64 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 64 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 66 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 66 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 68 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 68 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 70 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 70 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 72 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 72 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 74 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 74 + 1 + i * PointSizes[Section]] << 8) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 76 + i * PointSizes[Section], 4).Replace("-", string.Empty) + "\n";
                    }
                    break;
                //ITPT
                case 3:
                    csv += "#X,Y,Z,Point Size,Fly,Player Scan Radius\n";
                    for (int i = 0; i < numpt; i++)
                    {
                        csv += BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 8 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 12 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 16 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 20 + i * PointSizes[Section]) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 24 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 24 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 26 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 26 + 1 + i * PointSizes[Section]] << 8) + "\n";
                    }
                    break;
                //ITPH
                case 4:
                    csv += "#Start,Length,Previous1,Previous2,Previous3,Previous4,Previous5,Previous6,Next1,Next2,Next3,Next4,Next5,Next6\n";
                    for (int i = 0; i < numpt; i++)
                    {
                        csv += (short)(keiempi[Offsets[Section] + 0x58 + 8 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 8 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 10 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 10 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 12 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 12 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 14 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 14 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 16 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 16 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 18 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 18 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 20 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 20 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 22 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 22 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 24 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 24 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 26 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 26 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 28 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 28 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 30 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 30 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 32 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 32 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 34 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 34 + 1 + i * PointSizes[Section]] << 8) + "\n";
                    }
                    break;
                //CKPT
                case 5:
                    csv += "#X1,Z1,X2,Z2,Respawn,Type,Previous,Next,Clip ID,SectionCount,Unknown1,Unknown2\n";
                    for (int i = 0; i < numpt; i++)
                    {
                        csv += BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 8 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 12 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 16 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 20 + i * PointSizes[Section]) + ","
                             + keiempi[Offsets[Section] + 0x58 + 24 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 25 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 26 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 27 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 28 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 29 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 30 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 31 + i * PointSizes[Section]] + "\n";
                    }
                    break;
                //CKPH
                case 6:
                    csv += "#Start,Length,Previous1,Previous2,Previous3,Previous4,Previous5,Previous6,Next1,Next2,Next3,Next4,Next5,Next6,Unknown\n";
                    for (int i = 0; i < numpt; i++)
                    {
                        csv += keiempi[Offsets[Section] + 0x58 + 8 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 9 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 10 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 11 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 12 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 13 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 14 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 15 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 16 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 17 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 18 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 19 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 20 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 21 + i * PointSizes[Section]] + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 22 + i * PointSizes[Section], 2).Replace("-", string.Empty) + "\n";
                    }
                    break;
                //GOBJ
                case 7:
                    csv += "#Object ID,Unknown0,X,Y,Z,X Angle,Y Angle,Z Angle,X Scale,Y Scale,Z Scale,Route ID,Setting 1,Setting 2,Setting 3,Setting 4,Setting 5,Setting 6,Setting 7,Setting 8,Visibility,Unknown1,Unknown2\n";
                    for (int i = 0; i < numpt; i++)
                    {
                        csv += "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 8 + i * PointSizes[Section], 2).Replace("-", string.Empty) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 10 + i * PointSizes[Section], 2).Replace("-", string.Empty) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 12 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 16 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 20 + i * PointSizes[Section]) + ","
                             + Rad2Deg(BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 24 + i * PointSizes[Section])) + ","
                             + Rad2Deg(BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 28 + i * PointSizes[Section])) + ","
                             + Rad2Deg(BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 32 + i * PointSizes[Section])) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 36 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 40 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 44 + i * PointSizes[Section]) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 48 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 49 + i * PointSizes[Section]] << 8) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 50 + i * PointSizes[Section], 2).Replace("-", string.Empty) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 52 + i * PointSizes[Section], 2).Replace("-", string.Empty) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 54 + i * PointSizes[Section], 2).Replace("-", string.Empty) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 56 + i * PointSizes[Section], 2).Replace("-", string.Empty) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 58 + i * PointSizes[Section], 2).Replace("-", string.Empty) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 60 + i * PointSizes[Section], 2).Replace("-", string.Empty) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 62 + i * PointSizes[Section], 2).Replace("-", string.Empty) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 64 + i * PointSizes[Section], 2).Replace("-", string.Empty) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 66 + i * PointSizes[Section], 2).Replace("-", string.Empty) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 68 + i * PointSizes[Section], 2).Replace("-", string.Empty) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 70 + i * PointSizes[Section], 2).Replace("-", string.Empty) + "\n";
                    }
                    break;
                //POTI
                case 8:
                    short[,] usBy = new short[numpt, 2];
                    
                    //Initialize usBy
                    for (int r = 0; r < numpt; r++)
                        for (int c = 0; c < 2; c++)
                            usBy[r, c] = -1;

                    UsedBy(ref usBy);
                    byte numpt_ins;
                    int posit = Offsets[Section] + 8 + 0x58;
                    csv = csv_intro + "#" + SecMagic[1, Section] + "\n" + "#Amount of Sections: " + numpt.ToString() + "\n" + "#Total Amount of Points: " + numpoti.ToString() + "\n#NOTE! '$' Is a reserved character that indicates Route's sections Settings. You MUST put this BEFORE the section's points!\n";
                    for (int i = 0; i < numpt; i++)
                    {
                        numpt_ins = keiempi.Data[posit];
                        csv += "\n#Routes Section ID: " + i.ToString();
                        if (usBy[i, 1] == 0)
                            csv += "\n#Used by Object " + usBy[i, 0];
                        csv += "\n#Setting1,Setting2,Setting3\n";
                        csv += "$" + (keiempi.Data[posit + 1] & 0xFF).ToString() + "," + (keiempi.Data[posit + 2] & 0xFF).ToString() + "," + (keiempi.Data[posit + 3] & 0xFF).ToString() + "\n";
                        csv += "#Amount of Points: " + numpt_ins.ToString() + "\n";
                        csv += "#X,Y,Z,Unknown\n";
                        posit += 4;
                        for (int p = 0; p < numpt_ins; p++, posit += 16)
                        {
                            csv += BitConverter.ToSingle(keiempi.Data, posit) + ","
                                + BitConverter.ToSingle(keiempi.Data, posit + 4) + ","
                                + BitConverter.ToSingle(keiempi.Data, posit + 8) + ","
                                + "'" + BitConverter.ToString(keiempi.Data, posit + 12, 4).Replace("-", string.Empty) + "\n";
                        }
                    }
                    break;
                //AERA
                case 9:
                    csv += "#mode,type,EMACindex,Unknown0,X,Y,Z,X Angle,Y Angle,Z Angle,X Scale,Y Scale,Z Scale,settings 1,settings 2,Route ID,Enemy ID,unknown1\n";
                    for (int i = 0; i < numpt; i++)
                    {
                        csv += BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 8 + i * PointSizes[Section], 1) + ","
                             + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 9 + i * PointSizes[Section], 1) + ","
                             + (sbyte)keiempi[Offsets[Section] + 0x58 + 10 + i * PointSizes[Section]] + ","
                             + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 11 + i * PointSizes[Section], 1) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 12 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 16 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 20 + i * PointSizes[Section]) + ","
                             + Rad2Deg(BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 24 + i * PointSizes[Section])) + ","
                             + Rad2Deg(BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 28 + i * PointSizes[Section])) + ","
                             + Rad2Deg(BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 32 + i * PointSizes[Section])) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 36 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 40 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 44 + i * PointSizes[Section]) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 48 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 48 + 1 + i * PointSizes[Section]] << 8) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 50 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 50 + 1 + i * PointSizes[Section]] << 8) + ","
                             + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 52 + i * PointSizes[Section], 1) + ","
                             + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 53 + i * PointSizes[Section], 1) + ","
                             + (sbyte)keiempi[Offsets[Section] + 0x58 + 52 + i * PointSizes[Section]] + ","
                             + (sbyte)keiempi[Offsets[Section] + 0x58 + 53 + i * PointSizes[Section]] + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 54 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 58 + 1 + i * PointSizes[Section]] << 8) + "\n";
                    }
                    break;
                //JGPT
                case 11:
                    csv += "#X,Y,Z,X Angle,Y Angle,Z Angle,Index,Unknown\n";
                    for (int i = 0; i < numpt; i++)
                    {
                        csv += BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 8 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 12 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 16 + i * PointSizes[Section]) + ","
                             + Rad2Deg(BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 20 + i * PointSizes[Section])) + ","
                             + Rad2Deg(BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 24 + i * PointSizes[Section])) + ","
                             + Rad2Deg(BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 28 + i * PointSizes[Section])) + ","
                             + (short)(keiempi[Offsets[Section] + 0x58 + 32 + i * PointSizes[Section]] | keiempi[Offsets[Section] + 0x58 + 33 + i * PointSizes[Section]] << 8) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 34 + i * PointSizes[Section], 2).Replace("-", string.Empty) + "\n";
                    }
                    break;
                //GLPT
                case 16:
                    csv += "#X,Y,Z,Scale,Unknown1,Unknown2\n";
                    for (int i = 0; i < numpt; i++)
                    {
                        csv += BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 8 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 12 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 16 + i * PointSizes[Section]) + ","
                             + BitConverter.ToSingle(keiempi.Data, Offsets[Section] + 0x58 + 20 + i * PointSizes[Section]) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 24 + i * PointSizes[Section], 4).Replace("-", string.Empty) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 28 + i * PointSizes[Section], 4).Replace("-", string.Empty) + "\n";
                    }
                    break;
                //GLPH
                case 17:
                    csv += "#Start,Length,Previous1,Previous2,Previous3,Previous4,Previous5,Previous6,Next1,Next2,Next3,Next4,Next5,Next6,Unknown1,Unknown2\n";
                    for (int i = 0; i < numpt; i++)
                    {
                        csv += keiempi[Offsets[Section] + 0x58 + 8 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 9 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 10 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 11 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 12 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 13 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 14 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 15 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 16 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 17 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 18 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 19 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 20 + i * PointSizes[Section]] + ","
                             + keiempi[Offsets[Section] + 0x58 + 21 + i * PointSizes[Section]] + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 22 + i * PointSizes[Section], 4).Replace("-", string.Empty) + ","
                             + "'" + BitConverter.ToString(keiempi.Data, Offsets[Section] + 0x58 + 26 + i * PointSizes[Section], 4).Replace("-", string.Empty) + "\n";
                    }
                    break;
            }
            saveCSV.ShowDialog();
        }

        void ParseCSV()
        {
            int Section = comboBox1.SelectedIndex;
            int k = 0;
            int dollars = 0;
            var csv_lst = new List<string>();
            csv_lst.AddRange(csv_parse);
            for (; k < csv_lst.Count(); ) 
            {
                if ((csv_lst[k] == "") || (csv_lst[k].ToCharArray()[0] == '#'))
                    csv_lst.RemoveRange(k, 1);
                else
                    k++;
            }
            for (k = 0; k < csv_lst.Count(); k++ )
            {
                if (csv_lst[k].ToCharArray()[0] == '$')
                    dollars++;
            }
            csv_parse = csv_lst.ToArray();
            short numpt;
            short numpoti = 0;
            if (Section != 8)
                numpt = (short)csv_parse.Length;
            else
            {
                numpt = (short)dollars;
                numpoti = (short)(csv_parse.Length - dollars);
            }
            bool tf = true;
            int j;
            int lines = csv_parse.Length;
            if (Section != 8)
                for (j = 0; j < numpt && tf; j++)
                    tf = csv_parse[j].Split(',').Length == NumData[Section];
            else
            {
                for (j = 0; j < lines && tf; j++)
                {
                    tf = (csv_parse[j].Split(',').Length == NumData[Section]) || ((csv_parse[j].ToCharArray()[0] == '$') && (csv_parse[0].Split(',').Length>=3));
                }
            }

            if (tf)
            {
                //Magic+NumPt
                int saizu;
                if (Section != 8)
                    saizu = numpt * PointSizes[Section] + 8;
                else
                {
                    saizu = 8 + numpt * 4 + numpoti * 16;
                }
                byte[] SecData = new byte[saizu];
                SecData[0] = (byte)SecMagic[0, Section].ToCharArray()[0];
                SecData[1] = (byte)SecMagic[0, Section].ToCharArray()[1];
                SecData[2] = (byte)SecMagic[0, Section].ToCharArray()[2];
                SecData[3] = (byte)SecMagic[0, Section].ToCharArray()[3];
                SecData[4] = (byte)(numpt & 0xFF);
                SecData[5] = (byte)((numpt >> 8) & 0xFF);
                SecData[6] = (byte)(numpoti & 0xFF);
                SecData[7] = (byte)((numpoti >> 8) & 0xFF);
                //Serious stuff
                switch (Section)
                {
                    //KTPT
                    case 0:
                        for (int i = 0; i < numpt; i++)
                        {
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[0])), 0, SecData, i * PointSizes[Section] + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[1])), 0, SecData, i * PointSizes[Section] + 4 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[2])), 0, SecData, i * PointSizes[Section] + 8 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(Deg2Rad(float.Parse(csv_parse[i].Split(',')[3]))), 0, SecData, i * PointSizes[Section] + 12 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(Deg2Rad(float.Parse(csv_parse[i].Split(',')[4]))), 0, SecData, i * PointSizes[Section] + 16 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(Deg2Rad(float.Parse(csv_parse[i].Split(',')[5]))), 0, SecData, i * PointSizes[Section] + 20 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(Deg2Rad(int.Parse(csv_parse[i].Split(',')[6]))), 0, SecData, i * PointSizes[Section] + 24 + 8, 4);
                        }
                        break;
                    //ENPT
                    case 1:
                        for (int i = 0; i < numpt; i++)
                        {
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[0])), 0, SecData, i * PointSizes[Section] + 8 , 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[1])), 0, SecData, i * PointSizes[Section] + 4 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[2])), 0, SecData, i * PointSizes[Section] + 8 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[3])), 0, SecData, i * PointSizes[Section] + 12 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[4])), 0, SecData, i * PointSizes[Section] + 16 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[5])), 0, SecData, i * PointSizes[Section] + 18 + 8, 1);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[6])), 0, SecData, i * PointSizes[Section] + 19 + 8, 1);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[7])), 0, SecData, i * PointSizes[Section] + 20 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[8])), 0, SecData, i * PointSizes[Section] + 22 + 8, 2);
                        }
                        break;
                    //ENPH
                    case 2:
                        for (int i = 0; i < numpt; i++)
                        {
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[0])), 0, SecData, i * PointSizes[Section] + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[1])), 0, SecData, i * PointSizes[Section] + 2 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[2])), 0, SecData, i * PointSizes[Section] + 4 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[3])), 0, SecData, i * PointSizes[Section] + 6 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[4])), 0, SecData, i * PointSizes[Section] + 8 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[5])), 0, SecData, i * PointSizes[Section] + 10 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[6])), 0, SecData, i * PointSizes[Section] + 12 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[7])), 0, SecData, i * PointSizes[Section] + 14 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[8])), 0, SecData, i * PointSizes[Section] + 16 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[9])), 0, SecData, i * PointSizes[Section] + 18 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[10])), 0, SecData, i * PointSizes[Section] + 20 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[11])), 0, SecData, i * PointSizes[Section] + 22 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[12])), 0, SecData, i * PointSizes[Section] + 24 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[13])), 0, SecData, i * PointSizes[Section] + 26 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[14])), 0, SecData, i * PointSizes[Section] + 28 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[15])), 0, SecData, i * PointSizes[Section] + 30 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[16])), 0, SecData, i * PointSizes[Section] + 32 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[17])), 0, SecData, i * PointSizes[Section] + 34 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[18])), 0, SecData, i * PointSizes[Section] + 36 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[19])), 0, SecData, i * PointSizes[Section] + 38 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[20])), 0, SecData, i * PointSizes[Section] + 40 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[21])), 0, SecData, i * PointSizes[Section] + 42 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[22])), 0, SecData, i * PointSizes[Section] + 44 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[23])), 0, SecData, i * PointSizes[Section] + 46 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[24])), 0, SecData, i * PointSizes[Section] + 48 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[25])), 0, SecData, i * PointSizes[Section] + 50 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[26])), 0, SecData, i * PointSizes[Section] + 52 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[27])), 0, SecData, i * PointSizes[Section] + 54 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[28])), 0, SecData, i * PointSizes[Section] + 56 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[29])), 0, SecData, i * PointSizes[Section] + 58 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[30])), 0, SecData, i * PointSizes[Section] + 60 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[31])), 0, SecData, i * PointSizes[Section] + 62 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[32])), 0, SecData, i * PointSizes[Section] + 64 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[33])), 0, SecData, i * PointSizes[Section] + 66 + 8, 2);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[34]), 0, SecData, i * PointSizes[Section] + 68 + 8, 4);
                        }
                        break;
                    //ITPT
                    case 3:
                        for (int i = 0; i < numpt; i++)
                        {
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[0])), 0, SecData, i * PointSizes[Section] + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[1])), 0, SecData, i * PointSizes[Section] + 4 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[2])), 0, SecData, i * PointSizes[Section] + 8 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[3])), 0, SecData, i * PointSizes[Section] + 12 + 8, 4);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[4]), 0, SecData, i * PointSizes[Section] + 16 + 8, 4);
                        }
                        break;
                    //ITPH
                    case 4:
                        for (int i = 0; i < numpt; i++)
                        {
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[0])), 0, SecData, i * PointSizes[Section] + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[1])), 0, SecData, i * PointSizes[Section] + 2 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[2])), 0, SecData, i * PointSizes[Section] + 4 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[3])), 0, SecData, i * PointSizes[Section] + 6 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[4])), 0, SecData, i * PointSizes[Section] + 8 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[5])), 0, SecData, i * PointSizes[Section] + 10 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[6])), 0, SecData, i * PointSizes[Section] + 12 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[7])), 0, SecData, i * PointSizes[Section] + 14 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[8])), 0, SecData, i * PointSizes[Section] + 16 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[9])), 0, SecData, i * PointSizes[Section] + 18 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[10])), 0, SecData, i * PointSizes[Section] + 20 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[11])), 0, SecData, i * PointSizes[Section] + 22 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[12])), 0, SecData, i * PointSizes[Section] + 24 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[13])), 0, SecData, i * PointSizes[Section] + 26 + 8, 2);
                        }
                        break;
                    //CKPT
                    case 5:
                        for (int i = 0; i < numpt; i++)
                        {
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[0])), 0, SecData, i * PointSizes[Section] + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[1])), 0, SecData, i * PointSizes[Section] + 4 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[2])), 0, SecData, i * PointSizes[Section] + 8 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[3])), 0, SecData, i * PointSizes[Section] + 12 + 8, 4);
                            SecData[i * PointSizes[Section] + 16 + 8] = byte.Parse(csv_parse[i].Split(',')[4]);
                            SecData[i * PointSizes[Section] + 17 + 8] = byte.Parse(csv_parse[i].Split(',')[5]);
                            SecData[i * PointSizes[Section] + 18 + 8] = byte.Parse(csv_parse[i].Split(',')[6]);
                            SecData[i * PointSizes[Section] + 19 + 8] = byte.Parse(csv_parse[i].Split(',')[7]);
                            SecData[i * PointSizes[Section] + 20 + 8] = byte.Parse(csv_parse[i].Split(',')[8]);
                            SecData[i * PointSizes[Section] + 21 + 8] = byte.Parse(csv_parse[i].Split(',')[9]);
                            SecData[i * PointSizes[Section] + 22 + 8] = byte.Parse(csv_parse[i].Split(',')[10]);
                            SecData[i * PointSizes[Section] + 23 + 8] = byte.Parse(csv_parse[i].Split(',')[11]);
                        }
                        break;
                    //CKPH
                    case 6:
                        for (int i = 0; i < numpt; i++)
                        {
                            SecData[i * PointSizes[Section] + 8] = byte.Parse(csv_parse[i].Split(',')[0]);
                            SecData[i * PointSizes[Section] + 1 + 8] = byte.Parse(csv_parse[i].Split(',')[1]);
                            SecData[i * PointSizes[Section] + 2 + 8] = byte.Parse(csv_parse[i].Split(',')[2]);
                            SecData[i * PointSizes[Section] + 3 + 8] = byte.Parse(csv_parse[i].Split(',')[3]);
                            SecData[i * PointSizes[Section] + 4 + 8] = byte.Parse(csv_parse[i].Split(',')[4]);
                            SecData[i * PointSizes[Section] + 5 + 8] = byte.Parse(csv_parse[i].Split(',')[5]);
                            SecData[i * PointSizes[Section] + 6 + 8] = byte.Parse(csv_parse[i].Split(',')[6]);
                            SecData[i * PointSizes[Section] + 7 + 8] = byte.Parse(csv_parse[i].Split(',')[7]);
                            SecData[i * PointSizes[Section] + 8 + 8] = byte.Parse(csv_parse[i].Split(',')[8]);
                            SecData[i * PointSizes[Section] + 9 + 8] = byte.Parse(csv_parse[i].Split(',')[9]);
                            SecData[i * PointSizes[Section] + 10 + 8] = byte.Parse(csv_parse[i].Split(',')[10]);
                            SecData[i * PointSizes[Section] + 11 + 8] = byte.Parse(csv_parse[i].Split(',')[11]);
                            SecData[i * PointSizes[Section] + 12 + 8] = byte.Parse(csv_parse[i].Split(',')[12]);
                            SecData[i * PointSizes[Section] + 13 + 8] = byte.Parse(csv_parse[i].Split(',')[13]);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[14]), 0, SecData, i * PointSizes[Section] + 14 + 8, 2);
                        }
                        break;
                    //GOBJ
                    case 7:
                        for (int i = 0; i < numpt; i++)
                        {
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[0]), 0, SecData, i * PointSizes[Section] + 8, 2);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[1]), 0, SecData, i * PointSizes[Section] + 2 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[2])), 0, SecData, i * PointSizes[Section] + 4 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[3])), 0, SecData, i * PointSizes[Section] + 8 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[4])), 0, SecData, i * PointSizes[Section] + 12 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(Deg2Rad(float.Parse(csv_parse[i].Split(',')[5]))), 0, SecData, i * PointSizes[Section] + 16 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(Deg2Rad(float.Parse(csv_parse[i].Split(',')[6]))), 0, SecData, i * PointSizes[Section] + 20 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(Deg2Rad(float.Parse(csv_parse[i].Split(',')[7]))), 0, SecData, i * PointSizes[Section] + 24 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[8])), 0, SecData, i * PointSizes[Section] + 28 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[9])), 0, SecData, i * PointSizes[Section] + 32 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[10])), 0, SecData, i * PointSizes[Section] + 36 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[11])), 0, SecData, i * PointSizes[Section] + 40 + 8, 2);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[12]), 0, SecData, i * PointSizes[Section] + 42 + 8, 2);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[13]), 0, SecData, i * PointSizes[Section] + 44 + 8, 2);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[14]), 0, SecData, i * PointSizes[Section] + 46 + 8, 2);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[15]), 0, SecData, i * PointSizes[Section] + 48 + 8, 2);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[16]), 0, SecData, i * PointSizes[Section] + 50 + 8, 2);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[17]), 0, SecData, i * PointSizes[Section] + 52 + 8, 2);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[18]), 0, SecData, i * PointSizes[Section] + 54 + 8, 2);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[19]), 0, SecData, i * PointSizes[Section] + 56 + 8, 2);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[20]), 0, SecData, i * PointSizes[Section] + 58 + 8, 2);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[21]), 0, SecData, i * PointSizes[Section] + 60 + 8, 2);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[22]), 0, SecData, i * PointSizes[Section] + 62 + 8, 2);
                        }
                        break;
                    //POTI
                    case 8:
                        int posit = 8;
                        int prev = 0;
                        int numpt_ins = 0;
                        for (int i = 0; i < lines; i++)
                            {
                                if (csv_parse[i].ToCharArray()[0] == '$')
                                {
                                    if (prev != 0) SecData[prev] = (byte)numpt_ins;
                                    SecData[posit + 1] = byte.Parse(csv_parse[i].Split(',')[0].Replace("$",string.Empty));
                                    SecData[posit + 2] = byte.Parse(csv_parse[i].Split(',')[1]);
                                    SecData[posit + 3] = byte.Parse(csv_parse[i].Split(',')[2]);
                                    numpt_ins = 0;
                                    prev = posit;
                                    posit += 4;
                                }
                                else
                                {
                                    numpt_ins++;
                                    Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[0])), 0, SecData, posit, 4);
                                    Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[1])), 0, SecData, posit + 4, 4);
                                    Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[2])), 0, SecData, posit + 8, 4);
                                    Array.Copy(StringToByte(csv_parse[i].Split(',')[3]), 0, SecData, posit + 12, 4);
                                    posit += 16;
                                }
                            }
                        SecData[prev] = (byte)numpt_ins;
                        break;
                    //AERA
                    case 9:
                        for (int i = 0; i < numpt; i++)
                        {
                            SecData[i * PointSizes[Section] + 1 + 8] = byte.Parse(csv_parse[i].Split(',')[0]);
                            SecData[i * PointSizes[Section] + 2 + 8] = byte.Parse(csv_parse[i].Split(',')[1]);
                            SecData[i * PointSizes[Section] + 3 + 8] = (byte)sbyte.Parse(csv_parse[i].Split(',')[2]);
                            SecData[i * PointSizes[Section] + 4 + 8] = byte.Parse(csv_parse[i].Split(',')[3]);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[4])), 0, SecData, i * PointSizes[Section] + 8 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[5])), 0, SecData, i * PointSizes[Section] + 12 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[6])), 0, SecData, i * PointSizes[Section] + 16 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(Deg2Rad(float.Parse(csv_parse[i].Split(',')[7]))), 0, SecData, i * PointSizes[Section] + 20 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(Deg2Rad(float.Parse(csv_parse[i].Split(',')[8]))), 0, SecData, i * PointSizes[Section] + 24 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(Deg2Rad(float.Parse(csv_parse[i].Split(',')[9]))), 0, SecData, i * PointSizes[Section] + 28 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[10])), 0, SecData, i * PointSizes[Section] + 32 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[11])), 0, SecData, i * PointSizes[Section] + 36 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[12])), 0, SecData, i * PointSizes[Section] + 40 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[13])), 0, SecData, i * PointSizes[Section] + 42 + 8, 2);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[14])), 0, SecData, i * PointSizes[Section] + 44 + 8, 2);
                            SecData[i * PointSizes[Section] + 45 + 8] = (byte)sbyte.Parse(csv_parse[i].Split(',')[15]);
                            SecData[i * PointSizes[Section] + 46 + 8] = (byte)sbyte.Parse(csv_parse[i].Split(',')[16]);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[17])), 0, SecData, i * PointSizes[Section] + 46 + 8, 2);
                        }
                        break;
                    //JGPT
                    case 11:
                        for (int i = 0; i < numpt; i++)
                        {
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[0])), 0, SecData, i * PointSizes[Section] + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[1])), 0, SecData, i * PointSizes[Section] + 4 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[2])), 0, SecData, i * PointSizes[Section] + 8 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(Deg2Rad(float.Parse(csv_parse[i].Split(',')[3]))), 0, SecData, i * PointSizes[Section] + 12 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(Deg2Rad(float.Parse(csv_parse[i].Split(',')[4]))), 0, SecData, i * PointSizes[Section] + 16 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(Deg2Rad(float.Parse(csv_parse[i].Split(',')[5]))), 0, SecData, i * PointSizes[Section] + 20 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(short.Parse(csv_parse[i].Split(',')[6])), 0, SecData, i * PointSizes[Section] + 24 + 8, 2);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[7]), 0, SecData, i * PointSizes[Section] + 26 + 8, 2);
                        }
                        break;
                    //GLPT
                    case 16:
                        for (int i = 0; i < numpt; i++)
                        {
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[0])), 0, SecData, i * PointSizes[Section] + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[1])), 0, SecData, i * PointSizes[Section] + 4 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[2])), 0, SecData, i * PointSizes[Section] + 8 + 8, 4);
                            Array.Copy(BitConverter.GetBytes(float.Parse(csv_parse[i].Split(',')[3])), 0, SecData, i * PointSizes[Section] + 12 + 8, 4);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[4]), 0, SecData, i * PointSizes[Section] + 16 + 8, 4);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[5]), 0, SecData, i * PointSizes[Section] + 20 + 8, 4);
                        }
                        break;
                    //GLPH
                    case 17:
                        for (int i = 0; i < numpt; i++)
                        {
                            SecData[i * PointSizes[Section] + 8] = byte.Parse(csv_parse[i].Split(',')[0]);
                            SecData[i * PointSizes[Section] + 1 + 8] = byte.Parse(csv_parse[i].Split(',')[1]);
                            SecData[i * PointSizes[Section] + 2 + 8] = byte.Parse(csv_parse[i].Split(',')[2]);
                            SecData[i * PointSizes[Section] + 3 + 8] = byte.Parse(csv_parse[i].Split(',')[3]);
                            SecData[i * PointSizes[Section] + 4 + 8] = byte.Parse(csv_parse[i].Split(',')[4]);
                            SecData[i * PointSizes[Section] + 5 + 8] = byte.Parse(csv_parse[i].Split(',')[5]);
                            SecData[i * PointSizes[Section] + 6 + 8] = byte.Parse(csv_parse[i].Split(',')[6]);
                            SecData[i * PointSizes[Section] + 7 + 8] = byte.Parse(csv_parse[i].Split(',')[7]);
                            SecData[i * PointSizes[Section] + 8 + 8] = byte.Parse(csv_parse[i].Split(',')[8]);
                            SecData[i * PointSizes[Section] + 9 + 8] = byte.Parse(csv_parse[i].Split(',')[9]);
                            SecData[i * PointSizes[Section] + 10 + 8] = byte.Parse(csv_parse[i].Split(',')[10]);
                            SecData[i * PointSizes[Section] + 11 + 8] = byte.Parse(csv_parse[i].Split(',')[11]);
                            SecData[i * PointSizes[Section] + 12 + 8] = byte.Parse(csv_parse[i].Split(',')[12]);
                            SecData[i * PointSizes[Section] + 13 + 8] = byte.Parse(csv_parse[i].Split(',')[13]);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[14]), 0, SecData, i * PointSizes[Section] + 14 + 8, 4);
                            Array.Copy(StringToByte(csv_parse[i].Split(',')[15]), 0, SecData, i * PointSizes[Section] + 18 + 8, 4);
                        }
                        break;
                }
                Inject(SecData);
                MessageBox.Show(SecMagic[1, Section] + " injected successfully!");
            }
            else 
                if (Section != 8) MessageBox.Show("Computable Line " + j + " contains less fields than expected in a " + SecMagic[1, Section] + " Section.\nThe number of fields should be " + NumData[Section] + ".", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else MessageBox.Show("Computable Line " + j + " contains less fields than expected in a " + SecMagic[1, Section] + " Section.\nThe number of fields should be " + NumData[Section] + " or if it's a Section header (The ones followed by '$'), it must have at least 3 fields.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public KMPEd()
        {
            InitializeComponent();
        }

        private void OpenKMPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = openKMP.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                string path = openKMP.FileName;
                try
                {
                    keiempi = new KMP(File.ReadAllBytes(path));
                    SizeOfKMP = keiempi.Data.Length;
                    CheckHeader();
                    CheckSectPoint();
                }
                catch (IOException)
                {
                    MessageBox.Show("Error when reading from file!");
                }
            }
        }

        private void OpenKMP_FileOk(object sender, CancelEventArgs e)
        {}

        private void SaveFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            string name = saveKMP.FileName;
            File.Create(name).Close();
            File.WriteAllBytes(name, keiempi.Data);
        }

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveKMP.ShowDialog();
        }

        private void ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            extractbin.Enabled=true;
            injectbin.Enabled=true;
            if ((PointSizes[comboBox1.SelectedIndex] == 255) && (comboBox1.SelectedIndex != 8))
            {
                extractcsv.Enabled = false;
                injectcsv.Enabled = false;
            }
            else
            {
                extractcsv.Enabled = true;
                injectcsv.Enabled = true;
            }
        }

        private void Extractbin_Click(object sender, EventArgs e)
        {
            saveBinary.ShowDialog();
        }

        private void Injectbin_Click(object sender, EventArgs e)
        {
            DialogResult result = openBinary.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                string path = openBinary.FileName;
                try
                {
                    bin = new KMP(File.ReadAllBytes(path));
                    ImportBin();
                }
                catch (IOException)
                {
                    MessageBox.Show("Error when reading from file!");
                }
            }
        }

        private void Extractcsv_Click(object sender, EventArgs e)
        {
            ExportCSV();
        }

        private void Injectcsv_Click(object sender, EventArgs e)
        {
            DialogResult result = openCSV.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                string path = openCSV.FileName;
                try
                {
                    csv_parse = File.ReadAllLines(path);
                    ParseCSV();
                }
                catch (IOException)
                {
                    MessageBox.Show("Error when reading from file!");
                }
            }
        }

        private void SaveBinary_FileOk(object sender, CancelEventArgs e)
        {
            int size;
            if (comboBox1.SelectedIndex == 17)
                size = SizeOfKMP - (Offsets[comboBox1.SelectedIndex] + 0x58);
            else size = Offsets[comboBox1.SelectedIndex + 1] - Offsets[comboBox1.SelectedIndex];
            byte[] binary = new byte[size];
            Array.Copy(keiempi.Data, Offsets[comboBox1.SelectedIndex] + 0x58, binary, 0, binary.Length);
            string name = saveBinary.FileName;
            File.Create(name).Close();
            File.WriteAllBytes(name, binary);
        }

        private void Filesize_box_TextChanged(object sender, EventArgs e)
        {}

        private void SaveCSV_FileOk(object sender, CancelEventArgs e)
        {
            string name = saveCSV.FileName;
            File.Create(name).Close();
            File.WriteAllText(name, csv);
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("KMP Expander (" + Ver + ") - made by Ermelber\n\nThis tool is capable of editing KMP files by injecting binary or comma separated values (in combination with any Spreadsheet software).\n\nSpecial Thanks to Gericom for some programming tips.\n\n(c) 2015 Ermelber");
        }

        private void Label1_Click(object sender, EventArgs e)
        {}
        #endregion
    }
}
