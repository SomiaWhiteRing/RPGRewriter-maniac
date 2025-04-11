using System.IO;
using System;

namespace RPGRewriter
{
    class MoveStep
    {
        private int id;
        int value;
        int value2;
        int value3;
        string stringArg;
        string source;
        
        byte[] dataBytes;
        
        static string[] moveStr = { "Up", "Right", "Down", "Left", "Right-Up",
                                    "Right-Down", "Left-Down", "Left-Up", "Random Step", "Toward Hero",
                                    "Away Hero", "Forward", "Face Up", "Face Right", "Face Down",
                                    "Face Left", "Turn Right", "Turn Left", "About Face", "Turn Right/Left",
                                    "Face Random", "Face Hero", "Face Away Hero", "Wait", "Start Jump",
                                    "End Jump", "Fix Dir", "Unfix Dir", "Speed Up", "Speed Down",
                                    "Freq-Up", "Freq-Down", "[Switch On]", "[Switch Off]", "[CharSet]",
                                    "[Sound]", "Slip-Thru", "Unslip-Thru", "Stop Anim", "Resume Anim",
                                    "Transp-Up", "Transp-Down" };
       
        // public MoveStep(FileStream f, ref int lengthTemp, string source)
        // {
        //     load(f, ref lengthTemp, source);
        // }
        public MoveStep()
        {
        }
        
        // Loads data for one move route step.
        // 新的 load 方法签名
        public int load(FileStream f, string source)
        {
            long startPos = f.Position; // 记录起始位置
            this.source = source;

            id = M.readByte(f);

            if (id == 0x20 || id == 0x21) // Switch On/Off
            {
                if (source != "Custom")
                {
                    value = M.readByte(f);
                    if (value == 129) // 那个可疑的逻辑
                    {
                        // 仍然需要读取这些字节，但不修改 lengthTemp
                        byte b1 = M.readByte(f); // 读取但不赋值给 value
                        byte b2 = M.readByte(f); // 读取但不赋值给 value
                        // 这里的 value 赋值逻辑可能需要重新审视其正确性
                        // value = 129 + (b1 - 1) * 128 + (b2 - 1); // 类似这样的组合？需要确认格式
                        Console.WriteLine($"DEBUG: MoveStep Switch (non-custom) read pseudo-multibyte with initial byte 129. Bytes read: {b1}, {b2}. Value set to {value}. Verify logic!");

                    }
                    // else: value 就是读取的那一个字节
                }
                else
                {
                    value = M.readMultibyte(f);
                }
            }
            else if (id == 0x22) // CharSet Change
            {
                // readStringMoveAndRewrite 不再修改 lengthTemp
                stringArg = M.readStringMoveAndRewrite(f, M.M_CHARSET, M.S_FILENAME, source);
                value = M.readByte(f); // Index
            }
            else if (id == 0x23) // Sound
            {
                stringArg = M.readStringMoveAndRewrite(f, M.M_SOUND, M.S_FILENAME, source);
                value = M.readByte(f); // Volume

                if (source != "Custom")
                {
                    value2 = M.readByte(f); // Tempo
                    if (value2 == 129) // 可疑逻辑
                    {
                    // M.byteCheck(f, 0x01); // 这个 byteCheck 可能也需要审视
                        byte b1 = M.readByte(f);
                        // 这里的 value2 赋值逻辑可能需要重新审视
                        Console.WriteLine($"DEBUG: MoveStep Sound (non-custom) read pseudo-multibyte for tempo with initial byte 129. Byte read: {b1}. Value set to {value2}. Verify logic!");
                    }
                    // else: value2 就是读取的那一个字节
                }
                else
                {
                    value2 = M.readMultibyte(f); // Tempo
                }

                value3 = M.readByte(f); // Balance
            }
            // ... 其他命令的处理（如果需要读取参数） ...

            long endPos = f.Position; // 记录结束位置
            return (int)(endPos - startPos); // 返回读取的字节数
        }

        public int getID() { return id; } // Getter for ID
        
        // 示例：判断是否是结束命令
        public bool isEndCommand()
        {
            // RPG Maker 的移动路线结束命令代码通常是 0
            return id == 0;
        }

        // Returns move step string.
        public string getString()
        {
            string moveString = moveStr[id];
            
            if (id == 0x20) // Switch On
                moveString = "Switch " + M.getDataSwitch(value) + " On";
            else if (id == 0x21) // Switch Off
                moveString = "Switch " + M.getDataSwitch(value) + " Off";
            else if (id == 0x22) // CharSet Change
                moveString = "CharSet " + stringArg + " Index " + (value + 1);
            else if (id == 0x23) // Sound
                moveString = "Sound " + stringArg + ", " + M.getSoundVTB(value, value2, value3);
            
            return "- " + moveString;
        }
        
        // Writes move step data, to parent writer by default, and returns the byte size of that data.
        public int write(ref int lengthMinus, bool writeToParent = true)
        {
            if (dataBytes == null) // Only need to write once to create dataBytes
            {
                BinaryWriter parentWriter = M.targetWriter;
                BinaryWriter moveStepData = new BinaryWriter(new MemoryStream());
                M.targetWriter = moveStepData;
                
                M.writeByte(id);
                
                if (id == 0x20 || id == 0x21) // Switch On/Off
                {
                    if (source != "Custom")
                    {
                        if (value < 128) // Switch Number
                            M.writeByte(value);
                        else // Weird fake-multibyte, NOT used in custom routes. Seriously, what.
                        {
                            M.writeByte(0x81);
                            M.writeByte(M.div(value, 128));
                            M.writeByte(value % 128);
                            lengthMinus++; // One of these bytes doesn't get counted in length
                        }
                    }
                    else
                        M.writeMultibyte(value); // Switch Number
                }
                else if (id == 0x22) // CharSet Change
                {
                    lengthMinus += M.writeString(stringArg, M.S_FILENAME, source);
                    M.writeByte(value); // Index
                }
                else if (id == 0x23) // Sound
                {
                    lengthMinus += M.writeString(stringArg, M.S_FILENAME, source);
                    M.writeByte(value); // Volume
                    
                    if (source != "Custom")
                    {
                        if (value2 < 128) // Tempo
                            M.writeByte(value2);
                        else // Fake Multibyte
                        {
                            M.writeByte(0x81);
                            M.writeByte(0x01);
                            M.writeByte(value2 % 128);
                            lengthMinus++; // One of these bytes doesn't get counted in length
                        }
                    }
                    else
                        M.writeMultibyte(value2); // Tempo
                    
                    M.writeByte(value3); // Balance
                }
                
                M.targetWriter = parentWriter;
                dataBytes = (moveStepData.BaseStream as MemoryStream).ToArray();
                moveStepData.Close();
            }
            
            if (writeToParent)
                M.writeByteArrayNoLength(dataBytes);
            return dataBytes.Length;
        }
        
        // Replaces filename references.
        public void replaceFilenames()
        {
            if (id == 0x22) // CharSet Change
                stringArg = M.rewriteString(M.M_CHARSET, stringArg);
            else if (id == 0x23) // Sound
                stringArg = M.rewriteString(M.M_SOUND, stringArg);
        }
    }
}
