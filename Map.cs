using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace RPGRewriter
{
    class Map : RPGDataFile
    {
        string filepath = "";
        string filename = "";
        int id = 0;
        
        int tileset = 1; // 01
        int mapWidth = 20; // 02
        int mapHeight = 15; // 03
        int wrapType = 0; // 0b
        bool useParallax = false; // 1f
        string parallaxName = ""; // 20
        bool horzLoop = false; // 21
        bool vertLoop = false; // 22
        bool horzAuto = false; // 23
        int horzSpeed = 0; // 24
        bool vertAuto = false; // 25
        int vertSpeed = 0; // 26
        bool useRandomGenerator = false; // 28, verbose only (2003)
        int generatorMode = 0; // 29, verbose only (2003)
        bool topLevel = false; // 2a, verbose only (2003)
        int generatorGranularity = 0; // 30, verbose only (2003)
        int generatorRoomWidth = 4; // 31, verbose only (2003)
        int generatorRoomHeight = 1; // 32, verbose only (2003)
        bool generatorSurroundWithWalls = true; // 33, verbose only (2003)
        bool generatorUseUpperWall = true; // 34, verbose only (2003)
        bool generatorUseFloorB = true; // 35, verbose only (2003)
        bool generatorUseFloorC = true; // 36, verbose only (2003)
        bool generatorUseObstacleB = true; // 37, verbose only (2003)
        bool generatorUseObstacleC = true; // 38, verbose only (2003)
        long[] generatorTileX; // 3c, verbose only (2003)
        long[] generatorTileY; // 3d, verbose only (2003)
        long[] generatorTileID; // 3e, verbose only (2003)
        int[][] layer1Tiles; // 47, getTilesString or verbose only
        int[][] layer2Tiles; // 48, getTilesString or verbose only
        List<Event> events = new List<Event>(); // 51 - 初始化为空列表
        int saveCount2003E = 0; // 5a, verbose only
        int saveCount = 0; // 5b, verbose only
        
        static string myClass = "Map";
        Chunks chunks;
        
        static string[] wrapTypes = { "None", "Vertical Loop", "Horizontal Loop", "Both Loop" };
        static string[] generatorModes = { "Single Winding Passage", "Rooms Links With Passages",
                                           "Maze-Like Passage Structure", "Open Room with Obstacles" };
        static string[] generatorGranularities = { "1x1", "2x2" };
        static string[] tileNames = { "Ceiling", "Lower Wall", "Upper Wall", "Floor A", "Floor B", "Floor C",
                                      "Obstacle A", "Obstacle B", "Obstacle C" };
        
        public Map(string filepath, bool writeLog = true)
        {
            loadFile(filepath, writeLog);
        }
        public Map()
        {
        }
        
        // Loads a single .lmu map. Returns success.
        public bool loadFile(string filepath, bool writeLog = true)
        {
            if (!File.Exists(filepath))
            {
                Console.WriteLine("Map file " + filepath + " not found.");
                return false;
            }
            
            this.filepath = filepath;
            filename = Path.GetFileName(filepath);
            M.currentFile = filename;
            int.TryParse(filename.Replace("Map", "").Replace(".lmu", ""), out id);
            
            if (M.comparisonMode || M.extractDoubleMode)
                Console.WriteLine("Reading " + (M.readingOriginal? "original" : "translated") + " " + filename + "...");
            else if (M.copyingTileData || M.copyingCommandValues)
            {
                if (M.globalMode == "Extracting")
                    Console.WriteLine("Reading from source " + filename + "...");
            }
            else if (!M.stringScriptImportMode && M.globalMode != "Rewriting")
                Console.WriteLine(M.globalMode + " " + filename + "...");
            
            FileStream f = File.OpenRead(filepath);
            
            try
            {
                chunks = new Chunks(f, myClass);
                
                M.stringCheck(f, "LcfMapUnit");
                
                if (chunks.next(0x01))
                    tileset = M.readLengthMultibyte(f);
                if (chunks.next(0x02))
                    mapWidth = M.readLengthMultibyte(f);
                if (chunks.next(0x03))
                    mapHeight = M.readLengthMultibyte(f);
                
                if (chunks.next(0x0b))
                    wrapType = M.readLengthMultibyte(f);
                
                if (chunks.next(0x1f))
                    useParallax = M.readLengthBool(f);
                if (chunks.next(0x20))
                    parallaxName = M.readStringAndRewrite(f, M.M_PANORAMA, M.S_FILENAME);
                if (chunks.next(0x21))
                    horzLoop = M.readLengthBool(f);
                if (chunks.next(0x22))
                    vertLoop = M.readLengthBool(f);
                if (chunks.next(0x23))
                    horzAuto = M.readLengthBool(f);
                if (chunks.next(0x24))
                    horzSpeed = M.readLengthMultibyte(f);
                if (chunks.next(0x25))
                    vertAuto = M.readLengthBool(f);
                if (chunks.next(0x26))
                    vertSpeed = M.readLengthMultibyte(f);
                
                if (chunks.next(0x28))
                    useRandomGenerator = M.readLengthBool(f);
                if (chunks.next(0x29))
                    generatorMode = M.readLengthMultibyte(f);
                if (chunks.next(0x2a))
                    topLevel = M.readLengthBool(f);
                if (chunks.next(0x30))
                    generatorGranularity = M.readLengthMultibyte(f);
                if (chunks.next(0x31))
                    generatorRoomWidth = M.readLengthMultibyte(f);
                if (chunks.next(0x32))
                    generatorRoomHeight = M.readLengthMultibyte(f);
                if (chunks.next(0x33))
                    generatorSurroundWithWalls = M.readLengthBool(f);
                if (chunks.next(0x34))
                    generatorUseUpperWall = M.readLengthBool(f);
                if (chunks.next(0x35))
                    generatorUseFloorB = M.readLengthBool(f);
                if (chunks.next(0x36))
                    generatorUseFloorC = M.readLengthBool(f);
                if (chunks.next(0x37))
                    generatorUseObstacleB = M.readLengthBool(f);
                if (chunks.next(0x38))
                    generatorUseObstacleC = M.readLengthBool(f);
                
                if (chunks.next(0x3c))
                    generatorTileX = M.readFourByteArray(f);
                if (chunks.next(0x3d))
                    generatorTileY = M.readFourByteArray(f);
                if (chunks.next(0x3e))
                    generatorTileID = M.readFourByteArray(f);
                
                if (chunks.next(0x47))
                    layer1Tiles = M.readTwoByteArray2D(f, mapHeight, mapWidth);
                if (chunks.next(0x48))
                    layer2Tiles = M.readTwoByteArray2D(f, mapHeight, mapWidth);
                
                if (chunks.next(0x51)) // Events chunk ID
                {
                    int declaredEventChunkLength = M.readMultibyte(f); // 翻译软件可能改错这里
                    int declaredEventCount = M.readMultibyte(f);      // 事件数量通常是准确的
                    long startOfEventData = f.Position;
                    // *** 使用声明长度计算理论结束位置，但要意识到它可能错误 ***
                    long declaredEndOfChunk = startOfEventData + declaredEventChunkLength;
                    // *** 获取文件实际剩余长度作为硬性边界 ***
                    long actualEndOfFile = f.Length;
                    long safeEndOfChunk = Math.Min(declaredEndOfChunk, actualEndOfFile); // 不能读超过文件末尾

                    events = new List<Event>();
                    M.logMessage($"Map {id}: Declared Event Count: {declaredEventCount}, Declared Chunk Length: {declaredEventChunkLength} bytes. Data range: {M.hexParen(startOfEventData)} - {M.hexParen(declaredEndOfChunk)} (Safe limit: {M.hexParen(safeEndOfChunk)})");

                    for (int i = 0; i < declaredEventCount; i++) // 循环使用声明的事件数量
                    {
                        long eventStartPos = f.Position;
                        M.logMessage($"-- Map {id}: Attempting to load Event Index {i} at offset {M.hexParen(eventStartPos)} --");

                        // *** 关键安全检查：在尝试加载前，确保起始位置有效 ***
                        if (eventStartPos >= safeEndOfChunk) // 使用安全边界
                        {
                            M.logMessage($"Warning: Map {id} Event index {i}: Start position {M.hexParen(eventStartPos)} is at or beyond safe chunk end {M.hexParen(safeEndOfChunk)}. Stopping event read.");
                            break; // 停止读取
                        }

                        M.currentEvent = $"Map {id} Event Index {i}";
                        M.currentPage = ""; M.currentLine = "";
                        M.currentEventNum = 0; M.currentPageNum = 0;

                        Event currentEvent = null;
                        bool internalLoadThrewException = false;
                        bool tailByteCheckPassed = false;
                        bool recoveryNeeded = false;
                        bool eventAdded = false;
                        long positionAfterLoad = -1; // 记录加载后的位置

                        try
                        {
                            currentEvent = new Event();
                            currentEvent.load(f); // 尝试加载，可能抛异常

                            positionAfterLoad = f.Position; // 记录成功加载后的位置
                            M.logMessage($"   Map {id} Event {M.currentEventNum} (Index {i}): Internal load finished at {M.hexParen(positionAfterLoad)}.");

                            // 手动检查结尾字节，同样使用安全边界
                            if (positionAfterLoad < safeEndOfChunk)
                            {
                                byte tailByte = M.readByte(f);
                                if (tailByte == 0x00) {
                                    tailByteCheckPassed = true;
                                    M.logMessage($"   Map {id} Event {M.currentEventNum}: Tail byte check passed (0x00 found).");
                                } else {
                                    tailByteCheckPassed = false;
                                    recoveryNeeded = true;
                                    M.logMessage($"Warning: Map {id} Event {M.currentEventNum} (Index {i}): Tail byte check failed at offset {M.hexParen(positionAfterLoad)}. Expected 0x00, read {M.hexParen(tailByte)}. Recovery needed.");
                                }
                            } else {
                                tailByteCheckPassed = false;
                                recoveryNeeded = true;
                                M.logMessage($"Warning: Map {id} Event {M.currentEventNum} (Index {i}): Reached/exceeded safe chunk end ({M.hexParen(safeEndOfChunk)}) at offset {M.hexParen(positionAfterLoad)} when expecting tail byte 0x00. Recovery needed.");
                            }
                        }
                        catch (Exception ex)
                        {
                            internalLoadThrewException = true;
                            recoveryNeeded = true;
                            tailByteCheckPassed = false;
                            positionAfterLoad = f.Position; // 记录异常发生时的位置

                            int eventNumWithError = M.currentEventNum != 0 ? M.currentEventNum : (i + 1);
                            M.logMessage($"Error during Map {id} Event {eventNumWithError} (Index {i}) internal parsing starting near {M.hexParen(eventStartPos)}: {ex.Message}");
                            M.debugMessage(ex.StackTrace);
                        }

                        bool overallSuccess = !internalLoadThrewException && tailByteCheckPassed && currentEvent != null && currentEvent.IsSuccessfullyLoaded;

                        if (overallSuccess) {
                            events.Add(currentEvent);
                            eventAdded = true;
                            M.logMessage($"   Map {id} Event {M.currentEventNum}: Successfully loaded and added.");
                        } else if (!eventAdded) {
                            int eventNumWithError = M.currentEventNum != 0 ? M.currentEventNum : (i + 1);
                            M.logMessage($"Skipped adding Event {eventNumWithError} (Index {i}) from Map {id}.");
                            // 如果不是内部异常导致未添加，说明是结尾字节错了，也需要恢复
                            if (!internalLoadThrewException) recoveryNeeded = true;
                        }

                        if (recoveryNeeded)
                        {
                            long posBeforeRecovery = positionAfterLoad != -1 ? positionAfterLoad : f.Position; // Use position after load/error
                            int eventNumWithError = M.currentEventNum != 0 ? M.currentEventNum : (i + 1);
                            int nextEventNum = i + 2; // 计算下一个期望的事件编号 (索引+2)
                            M.logMessage($"   Map {id} Event {eventNumWithError} (Index {i}): Recovery needed, attempting skip starting from {M.hexParen(posBeforeRecovery)}...");

                            // *** 传递 safeEndOfChunk 作为恢复的边界 ***
                            bool recoverySucceeded = M.TrySkipToNextEventStart(f, i, declaredEventCount, safeEndOfChunk);

                            if (!recoverySucceeded) {
                                M.logMessage($"Recovery failed after error at index {i}. Aborting reading remaining events in Map {id}.");
                                // *** 尝试将指针设置到 *声明* 的块尾或安全块尾中较小者 ***
                                f.Position = Math.Min(declaredEndOfChunk, safeEndOfChunk);
                                break;
                            } else {
                                long posAfterRecovery = f.Position;
                                M.logMessage($"   Map {id}: Recovery function returned success, stream position moved to {M.hexParen(posAfterRecovery)}.");
                                if (posAfterRecovery <= posBeforeRecovery && nextEventNum <= declaredEventCount) { // 增加检查，如果指针没前进且不是最后一个事件恢复
                                    M.logMessage($"Critical Error: Recovery function success but stream position did not advance significantly ({M.hexParen(posBeforeRecovery)} -> {M.hexParen(posAfterRecovery)}) for Event {eventNumWithError}. Aborting map.");
                                    f.Position = Math.Min(declaredEndOfChunk, safeEndOfChunk);
                                    break;
                                }
                                M.logMessage($"   Continuing loop for next event index {i + 1}.");
                            }
                        }
                        M.logMessage($"-- Map {id}: Finished processing Event Index {i}. Current file position: {M.hexParen(f.Position)} --");

                    } // End of for loop

                    // Final position check - 调整指针到 *声明* 的结束位置或安全位置
                    long finalExpectedPos = Math.Min(declaredEndOfChunk, safeEndOfChunk);
                    if (f.Position < finalExpectedPos) {
                        M.logMessage($"Warning: Map {id}: Finished events loop, position {M.hexParen(f.Position)} before expected end {M.hexParen(finalExpectedPos)}. Setting position to end.");
                        f.Position = finalExpectedPos;
                    } else if (f.Position > finalExpectedPos) {
                        M.logMessage($"Critical Warning: Map {id}: Position {M.hexParen(f.Position)} is BEYOND expected event chunk end {M.hexParen(finalExpectedPos)}.");
                        // 也许也强制设置回 finalExpectedPos？
                        f.Position = finalExpectedPos;
                    }
                } // End of if (chunks.next(0x51))
                
                if (chunks.next(0x5a))
                    saveCount2003E = M.readLengthMultibyte(f);
                if (chunks.next(0x5b))
                    saveCount = M.readLengthMultibyte(f);
                
                // M.byteCheck(f, 0x00);
                
                f.Close();
            }
            catch (Exception e)
            {
                M.debugMessage(e.StackTrace);
                M.debugMessage(e.Message);
                Console.WriteLine("Aborting due to error.");
                
                f.Close();
                return false;
            }
            
            if (writeLog)
                M.logSave();
            return true;
        }
        
        // Returns map string.
        public string getString()
        {
            M.currentFile = filename;
            
            StringWriter mapHeader = new StringWriter(new StringBuilder());
            StringWriter mapSettings = new StringWriter(new StringBuilder());
            StringWriter eventText = new StringWriter(new StringBuilder());
            
            if (!M.stringScriptExportMode)
                mapHeader.WriteLine("========== " + M.getDataMap(id) + " ==========");
            else if (M.getDetailSetting("MapHeader"))
            {
                mapHeader.WriteLine("==================== " + M.getDataMap(id) + " ====================");
                if (!M.getDetailSetting("MapSettings")) // Separate map header from first event header unless settings are included
                    mapHeader.WriteLine();
            }
            
            mapSettings.WriteLine("Tileset: " + M.getDataChipSet(tileset));
            mapSettings.WriteLine("Map Size: " + mapWidth + "x" + mapHeight);
            mapSettings.WriteLine("Wrap Type: " + wrapTypes[wrapType]);
            if (useParallax)
            {
                mapSettings.WriteLine("Parallax: " + parallaxName
                    + (horzLoop? (", Horizontal Loop" + (horzAuto? " (" + horzSpeed + ")" : "")) : "")
                    + (vertLoop? (", Vertical Loop" + (vertAuto? " (" + vertSpeed + ")" : "")) : ""));
            }
            mapSettings.WriteLine();
            
            if (M.superVerboseStrings)
            {
                if (useRandomGenerator)
                {
                    bool[] tileUsed = { true, true, generatorUseUpperWall, true, generatorUseFloorB, generatorUseFloorC,
                                        true, generatorUseObstacleB, generatorUseObstacleC };
                    
                    mapSettings.WriteLine("Dungeon Generator Mode: " + generatorModes[generatorMode]);
                    mapSettings.WriteLine("Top Level: " + topLevel);
                    mapSettings.WriteLine("Passage Granularity: " + generatorGranularities[generatorGranularity]);
                    mapSettings.WriteLine("Room Dimensions: " + generatorRoomWidth + "x" + generatorRoomHeight);
                    mapSettings.WriteLine("Surround Map With Wall Tiles: " + generatorSurroundWithWalls);
                    
                    for (int i = 0; i < tileNames.Length; i++)
                        if (tileUsed[i])
                            mapSettings.WriteLine(tileNames[i] + " Tile: " + generatorTileX[i] + "," + generatorTileY[i] + " (ID: " + generatorTileID[i] + ")");
                    
                    mapSettings.WriteLine();
                }
                
                mapSettings.WriteLine("Save Count: " + (saveCount != 0? saveCount : saveCount2003E));
                mapSettings.WriteLine();
                
                mapSettings.WriteLine(getTilesString(true));
            }
            
            for (int i = 0; i < events.Count; i++)
            {
                string thisEvText = events[i].getString();
                if (M.includeActions || thisEvText != "") // Include all events for actions, but leave out blanks if only messages.
                    eventText.WriteLine(thisEvText);
            }
            
            return mapHeader
                 + (M.includeActions || (M.stringScriptExportMode && M.getDetailSetting("MapSettings"))? mapSettings.ToString() : "")
                 + eventText;
        }
        
        // Returns string of tile data.
        public string getTilesString(bool condensed = false)
        {
            StringWriter tileText = new StringWriter(new StringBuilder());
            
            tileText.WriteLine("* LAYER 1 *");
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    int tile = layer1Tiles[y][x];
                    string str = (!condensed? (x + "," + y + ": ") : "") + getTileString(tile, condensed);
                    if (!condensed || x == mapWidth - 1)
                        tileText.WriteLine(str);
                    else
                        tileText.Write(str + " ");
                }
            }
            tileText.WriteLine();
            
            tileText.WriteLine("* LAYER 2 *");
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    int tile = layer2Tiles[y][x];
                    string str = (!condensed? (x + "," + y + ": ") : "") + getTileString(tile, condensed);
                    if (!condensed || x == mapWidth - 1)
                        tileText.WriteLine(str);
                    else
                        tileText.Write(str + " ");
                }
            }
            
            return tileText.ToString();
        }
        
        // Get description of tile given the number representation. ...I mean, in theory.
        string getTileString(int tile, bool numberOnly = false)
        {
            if (numberOnly)
                return tile.ToString();
            
            /*bool shadowUL = (tile & 1) != 0;
            bool shadowUR = (tile & 2) != 0;
            bool shadowBR = (tile & 4) != 0;
            bool shadowBL = (tile & 8) != 0;
                
            string shadowStr = (shadowUL? "UL " : "") + (shadowUR? "UR " : "") + (shadowBR? "BR " : "") + (shadowBL? "BL " : "");
            if (shadowStr != "")
                shadowStr = " (Shadow " + shadowStr.Trim() + ")";*/
            
            return "Tile " + tile;
        }
        
        // Replaces strings from importingStringArgs.
        public void importStrings()
        {
            foreach (Event ev in events)
                ev.importStrings();
        }
        
        // Saves map file from stored data.
        public bool writeFile()
        {
            if (M.fileInUse(filepath))
            {
                Console.WriteLine(filename + " is in use; cannot save.");
                return false;
            }
            
            File.Delete(filepath + ".bak");
            File.Move(filepath, filepath + ".bak");
            BinaryWriter mapWriter = new BinaryWriter(new FileStream(filepath, FileMode.Create));
            M.targetWriter = mapWriter;
            
            try
            {
                M.writeString("LcfMapUnit", M.S_CONSTANT);
                
                if (chunks.wasNext(0x01))
                    M.writeLengthMultibyte(tileset);
                if (chunks.wasNext(0x02))
                    M.writeLengthMultibyte(mapWidth);
                if (chunks.wasNext(0x03))
                    M.writeLengthMultibyte(mapHeight);
                
                if (chunks.wasNext(0x0b))
                    M.writeLengthMultibyte(wrapType);
                
                if (chunks.wasNext(0x1f))
                    M.writeLengthBool(useParallax);
                if (chunks.wasNext(0x20))
                    M.writeString(parallaxName, M.S_FILENAME);
                if (chunks.wasNext(0x21))
                    M.writeLengthBool(horzLoop);
                if (chunks.wasNext(0x22))
                    M.writeLengthBool(vertLoop);
                if (chunks.wasNext(0x23))
                    M.writeLengthBool(horzAuto);
                if (chunks.wasNext(0x24))
                    M.writeLengthMultibyte(horzSpeed);
                if (chunks.wasNext(0x25))
                    M.writeLengthBool(vertAuto);
                if (chunks.wasNext(0x26))
                    M.writeLengthMultibyte(vertSpeed);
                
                if (chunks.wasNext(0x28))
                    M.writeLengthBool(useRandomGenerator);
                if (chunks.wasNext(0x29))
                    M.writeLengthMultibyte(generatorMode);
                if (chunks.wasNext(0x2a))
                    M.writeLengthBool(topLevel);
                if (chunks.wasNext(0x30))
                    M.writeLengthMultibyte(generatorGranularity);
                if (chunks.wasNext(0x31))
                    M.writeLengthMultibyte(generatorRoomWidth);
                if (chunks.wasNext(0x32))
                    M.writeLengthMultibyte(generatorRoomHeight);
                if (chunks.wasNext(0x33))
                    M.writeLengthBool(generatorSurroundWithWalls);
                if (chunks.wasNext(0x34))
                    M.writeLengthBool(generatorUseUpperWall);
                if (chunks.wasNext(0x35))
                    M.writeLengthBool(generatorUseFloorB);
                if (chunks.wasNext(0x36))
                    M.writeLengthBool(generatorUseFloorC);
                if (chunks.wasNext(0x37))
                    M.writeLengthBool(generatorUseObstacleB);
                if (chunks.wasNext(0x38))
                    M.writeLengthBool(generatorUseObstacleC);
                
                if (chunks.wasNext(0x3c))
                    M.writeFourByteArray(generatorTileX);
                if (chunks.wasNext(0x3d))
                    M.writeFourByteArray(generatorTileY);
                if (chunks.wasNext(0x3e))
                    M.writeFourByteArray(generatorTileID);
                
                if (chunks.wasNext(0x47))
                    M.writeTwoByteArray2D(layer1Tiles);
                if (chunks.wasNext(0x48))
                    M.writeTwoByteArray2D(layer2Tiles);
                
                if (chunks.wasNext(0x51))
                    M.writeList<Event>(events);
                
                if (chunks.wasNext(0x5a))
                    M.writeLengthMultibyte(saveCount2003E);
                if (chunks.wasNext(0x5b))
                    M.writeLengthMultibyte(saveCount);
                
                M.writeByte(0x00);
                
                mapWriter.Close();
                M.targetWriter.Close();
                File.Delete(filepath + ".bak");
            }
            catch (Exception e)
            {
                M.debugMessage(e.StackTrace);
                M.debugMessage(e.Message);
                Console.WriteLine("Aborting due to error; keeping original file.");
                
                // Close file and restore backup.
                mapWriter.Close();
                M.targetWriter.Close();
                File.Delete(filepath);
                File.Move(filepath + ".bak", filepath);
                return false;
            }
            
            return true;
        }
        
        // Returns the Layer 1 tile array.
        public int[][] getLayer1Tiles()
        {
            return layer1Tiles;
        }
        
        // Returns the Layer 2 tile array.
        public int[][] getLayer2Tiles()
        {
            return layer2Tiles;
        }
        
        // Pastes tile data from another map into these tiles. Returns whether tiles were actually changed.
        public bool pasteTiles(int[][] newLayer1, int[][] newLayer2, string filename)
        {
            int myWidth = layer1Tiles[0].Length;
            int myHeight = layer1Tiles.Length;
            int newWidth = newLayer1[0].Length;
            int newHeight = newLayer1.Length;
            
            if (newWidth == myWidth && newHeight == myHeight)
            {
                bool exactMatch = true;
                for (int y = 0; y < myHeight; y++)
                {
                    for (int x = 0; x < myWidth; x++)
                    {
                        if (layer1Tiles[y][x] != newLayer1[y][x]
                         || layer2Tiles[y][x] != newLayer2[y][x])
                        {
                            exactMatch = false;
                            break;
                        }
                    }
                }
                
                if (!exactMatch)
                {
                    layer1Tiles = newLayer1;
                    layer2Tiles = newLayer2;
                    chunks.add(0x47);
                    chunks.add(0x48);
                    return true;
                }
                else
                    return false;
            }
            else
            {
                Console.WriteLine(filename + ": Map size mismatch. (Source: " + newWidth + "x" + newHeight
                    + ", destination: " + myWidth + "x" + myHeight + ")");
                return false;
            }
        }
    }
}
