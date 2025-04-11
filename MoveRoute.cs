using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace RPGRewriter
{
    class MoveRoute
    {
        List<MoveStep> steps;
        
        byte[] dataBytes;
        int lengthMinus = 0;
        
        public MoveRoute(FileStream f, int lengthTemp, string source)
        {
            load(f, lengthTemp, source);
        }
        public MoveRoute()
        {
        }
        
        // Loads move route data. Source can be "Move" (move command) or "Custom" (event page custom route); these two read some things in different ways.
        public void load(FileStream f, int expectedLength, string source)
        {
            steps = new List<MoveStep>();
            int bytesReadSoFar = 0;
            long routeStartPos = f.Position; // 记录开始位置
            bool encounteredEndCommand = false; // 标记是否遇到了结束命令

            // 主要循环条件：读取够预期的字节数
            while (bytesReadSoFar < expectedLength)
            {
                long stepStartPos = f.Position;
                MoveStep step = new MoveStep();
                int stepBytesRead = step.load(f, source);

                // 检查读取是否有效
                if (stepBytesRead <= 0)
                {
                    Console.WriteLine($"ERROR: MoveStep.load returned {stepBytesRead} bytes read. Aborting route load. Position: {f.Position}");
                    break; // 避免无限循环
                }

                bytesReadSoFar += stepBytesRead;
                steps.Add(step);

                Console.WriteLine($"DEBUG: MoveRoute read step {step.getID()}, bytes: {stepBytesRead}, total read: {bytesReadSoFar}, expected: {expectedLength}");

                // 记录是否遇到结束命令，但不再用它来 break 循环
                if (step.isEndCommand())
                {
                    encounteredEndCommand = true;
                    Console.WriteLine($"DEBUG: MoveRoute encountered end command (ID 0) at byte offset {bytesReadSoFar - stepBytesRead} within route.");
                    // 如果我们 *确定* 结束命令一定是最后一个字节，可以在这里加个检查：
                    // if (bytesReadSoFar != expectedLength) {
                    //    Console.WriteLine($"WARNING: End command encountered, but total bytes read ({bytesReadSoFar}) does not match expected length ({expectedLength}).");
                    // }
                    // 但暂时不 break，让循环继续直到字节数满足 expectedLength
                }

                // 安全检查：如果读取字节超出预期，立即停止并报告错误
                if (bytesReadSoFar > expectedLength)
                {
                    Console.WriteLine($"ERROR: MoveRoute read {bytesReadSoFar} bytes, exceeding expected {expectedLength}. Last step ID: {step.getID()}. Aborting.");
                    // 可能需要回退文件指针到预期结束位置
                    // f.Seek(routeStartPos + expectedLength, SeekOrigin.Begin);
                    // bytesReadSoFar = expectedLength; // 强制设置为预期长度以便后续检查通过？（取决于错误处理策略）
                    break;
                }
            }

            // 循环结束后严格检查字节数
            if (bytesReadSoFar == expectedLength)
            {
                Console.WriteLine($"DEBUG: MoveRoute successfully loaded {bytesReadSoFar} bytes as expected.");
                // 可以选择性地检查是否真的遇到了结束命令（如果格式要求必须有）
                // if (!encounteredEndCommand && expectedLength > 0) { // 如果路线非空但没遇到0
                //     Console.WriteLine($"WARNING: MoveRoute finished reading {expectedLength} bytes but did not encounter an end command (ID 0).");
                // }
            }
            else // bytesReadSoFar < expectedLength (因为 > 的情况在循环内处理了)
            {
                Console.WriteLine($"WARNING: MoveRoute loaded only {bytesReadSoFar} bytes, but expected {expectedLength}. File: {M.currentFile}, Event: {M.currentEventNum}, Page: {M.currentPageNum}");
                // 在这种情况下，文件指针可能停在错误的位置，需要处理
                long currentPos = f.Position;
                long expectedEndPos = routeStartPos + expectedLength;
                Console.WriteLine($"Current file pointer: {currentPos}, Expected end position: {expectedEndPos}");
                // 可以尝试强制将指针移动到预期位置，但这有风险
                // if (currentPos < expectedEndPos) {
                //     Console.WriteLine($"Attempting to seek forward to expected end position.");
                //     try {
                //         f.Seek(expectedEndPos, SeekOrigin.Begin);
                //         Console.WriteLine($"Seek successful. New position: {f.Position}");
                //     } catch (Exception seekEx) {
                //         Console.WriteLine($"ERROR: Failed to seek: {seekEx.Message}");
                //     }
                // }
            }
        }

        // Returns move route string.
        public string getString()
        {
            StringWriter allMovesStr = new StringWriter(new StringBuilder());
            for (int i = 0; i < steps.Count; i++)
                allMovesStr.Write(steps[i].getString() + (i < steps.Count - 1? Environment.NewLine : ""));
            return allMovesStr.ToString();
        }
        
        // Writes move route data, to parent writer by default, and returns the byte size of that data.
        public int write(bool writeToParent = true)
        {
            if (dataBytes == null) // Only need to write once to create dataBytes
            {
                BinaryWriter parentWriter = M.targetWriter;
                BinaryWriter moveRouteData = new BinaryWriter(new MemoryStream());
                M.targetWriter = moveRouteData;
                
                lengthMinus = 0;
                
                foreach (MoveStep step in steps)
                    step.write(ref lengthMinus);
                
                M.targetWriter = parentWriter;
                dataBytes = (moveRouteData.BaseStream as MemoryStream).ToArray();
                moveRouteData.Close();
            }
            
            if (writeToParent)
                M.writeByteArrayNoLength(dataBytes);
            return dataBytes.Length - lengthMinus;
        }
        
        // Returns the byte length of the move route data.
        public int getLength()
        {
            return write(false);
        }
        
        // Replaces filename references.
        public void replaceFilenames()
        {
            foreach (MoveStep step in steps)
                step.replaceFilenames();
        }
    }
}
