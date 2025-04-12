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
                    // 读取事件块元数据
                    int eventChunkLength = M.readMultibyte(f);
                    int eventCount = M.readMultibyte(f);
                    long startOfEventData = f.Position;
                    long calculatedEndOfChunk = startOfEventData + eventChunkLength;

                    // 确保 events 列表在使用前已初始化
                    if (events == null) {
                        events = new List<Event>();
                    } else {
                        events.Clear(); // 清空可能存在的旧数据
                    }

                    M.logMessage($"Map {id}: Found {eventCount} events. Chunk length: {eventChunkLength} bytes. Data range: {M.hexParen(startOfEventData)} - {M.hexParen(calculatedEndOfChunk)}");

                    for (int i = 0; i < eventCount; i++)
                    {
                        long eventStartPos = f.Position;
                        M.logMessage($"-- Map {id}: Attempting to load Event Index {i} at offset {M.hexParen(eventStartPos)} --");

                        // 安全检查：确保我们没有意外地超出数据块边界
                        if (eventStartPos >= calculatedEndOfChunk)
                        {
                            M.logMessage($"Warning: Map {id} Event index {i}: Reached or passed end of event chunk ({M.hexParen(calculatedEndOfChunk)}) prematurely at {M.hexParen(eventStartPos)}. Stopping event read.");
                            break; // 停止读取此地图的事件
                        }

                        // 设置当前上下文，用于日志记录和可能的错误报告
                        M.currentEvent = $"Map {id} Event Index {i}";
                        M.currentPage = ""; M.currentLine = "";
                        M.currentEventNum = 0; M.currentPageNum = 0; // 重置，Event.load内部会设置

                        Event currentEvent = null;
                        bool internalLoadThrewException = false; // Did Event.load itself throw an exception?
                        bool tailByteCheckPassed = false;      // Did the 0x00 byte check pass?
                        bool recoveryNeeded = false;         // Should we attempt recovery?
                        bool eventAdded = false;             // Was this event successfully added?
                        bool recoveryAttemptedAndFailed = false; // Track if recovery was tried and failed

                        try
                        {
                            currentEvent = new Event();
                            // *** 确保 Event.load 末尾的 byteCheck(f, 0x00) 已移除 ***
                            currentEvent.load(f); // 尝试加载事件数据，内部 byteCheck 失败会抛异常

                            // *** 手动检查事件结尾字节 ***
                            long positionAfterLoad = f.Position;
                            M.logMessage($"   Map {id} Event {M.currentEventNum} (Index {i}): Internal load finished at {M.hexParen(positionAfterLoad)}.");

                            if (positionAfterLoad < calculatedEndOfChunk)
                            {
                                byte tailByte = M.readByte(f); // 读取预期的 0x00
                                if (tailByte == 0x00)
                                {
                                    tailByteCheckPassed = true; // 结尾字节正确
                                    M.logMessage($"   Map {id} Event {M.currentEventNum}: Tail byte check passed (0x00 found).");
                                }
                                else
                                {
                                    // 结尾字节错误
                                    tailByteCheckPassed = false;
                                    recoveryNeeded = true; // 需要恢复，因为指针不正确
                                    M.logMessage($"Warning: Map {id} Event {M.currentEventNum} (Index {i}): Tail byte check failed at offset {M.hexParen(positionAfterLoad)}. Expected 0x00, read {M.hexParen(tailByte)}. Recovery needed.");
                                }
                            }
                            else
                            {
                                // 恰好在块尾或超出，结尾检查也算失败
                                tailByteCheckPassed = false;
                                recoveryNeeded = true; // 也需要恢复/调整指针
                                M.logMessage($"Warning: Map {id} Event {M.currentEventNum} (Index {i}): Reached/exceeded chunk end ({M.hexParen(calculatedEndOfChunk)}) at offset {M.hexParen(positionAfterLoad)} when expecting event end byte 0x00. Recovery needed.");
                            }
                        }
                        catch (Exception ex) // 捕获 Event.load 内部的异常
                        {
                            internalLoadThrewException = true;
                            recoveryNeeded = true; // 需要恢复
                            tailByteCheckPassed = false; // 不能假设结尾字节正确

                            int eventNumWithError = M.currentEventNum != 0 ? M.currentEventNum : (i + 1);
                            M.logMessage($"Error during Map {id} Event {eventNumWithError} (Index {i}) internal parsing starting near {M.hexParen(eventStartPos)}: {ex.Message}");
                            M.debugMessage(ex.StackTrace);
                            // loadSuccess 隐含为 false
                        }

                        // 最终成功条件：内部加载未抛异常 + 结尾字节检查通过 + Event对象自己确认加载完成
                        bool overallSuccess = !internalLoadThrewException && tailByteCheckPassed && currentEvent != null && currentEvent.IsSuccessfullyLoaded;

                        if (overallSuccess)
                        {
                            events.Add(currentEvent);
                            eventAdded = true;
                            M.logMessage($"   Map {id} Event {M.currentEventNum}: Successfully loaded and added.");
                        }

                        // 如果需要恢复（因为异常或结尾字节错误）
                        if (recoveryNeeded)
                        {
                            int eventNumWithError = M.currentEventNum != 0 ? M.currentEventNum : (i + 1);
                            M.logMessage($"   Map {id} Event {eventNumWithError}: Attempting recovery...");
                            if (!M.TrySkipToNextEventStart(f, i, eventCount, calculatedEndOfChunk))
                            {
                                recoveryAttemptedAndFailed = true; // 标记恢复失败
                                M.logMessage($"Recovery failed after error in Event {eventNumWithError}. Aborting reading remaining events in Map {id}.");
                                f.Position = calculatedEndOfChunk; // 强制指针到块尾
                                break; // 停止处理此地图的事件
                            }
                            else
                            {
                                M.logMessage($"   Map {id}: Recovery successful, continuing to next event index.");
                            }
                        }

                        // 记录跳过信息（仅当未添加且恢复未尝试或失败时）
                        if (!eventAdded)
                        {
                            int eventNumWithError = M.currentEventNum != 0 ? M.currentEventNum : (i + 1);
                            // 只有在不需要恢复（说明是结尾字节问题但恢复逻辑没触发？）或者恢复失败时才记录，避免重复日志
                            if (!recoveryNeeded || recoveryAttemptedAndFailed)
                                M.logMessage($"Skipped adding Event {eventNumWithError} (Index {i}) from Map {id} due to load failure or incorrect end byte.");
                        }

                        M.logMessage($"-- Map {id}: Finished processing Event Index {i}. Current file position: {M.hexParen(f.Position)} --");

                    } // End of for loop

                    // 循环结束后，最终检查并调整文件指针
                    if (f.Position < calculatedEndOfChunk)
                    {
                        M.logMessage($"Warning: Map {id}: Finished reading events loop, position {M.hexParen(f.Position)} is before expected end {M.hexParen(calculatedEndOfChunk)}. Setting position to end.");
                        f.Position = calculatedEndOfChunk;
                    }
                    else if (f.Position > calculatedEndOfChunk)
                    {
                        // 这种情况通常意味着 TrySkipToNextEventStart 可能有 bug 或数据损坏极度严重
                        M.logMessage($"Critical Warning: Map {id}: Position {M.hexParen(f.Position)} is BEYOND expected event chunk end {M.hexParen(calculatedEndOfChunk)}. Subsequent map data might be corrupted.");
                    }
                    else {
                        M.logMessage($"Map {id}: Event processing finished. Final position {M.hexParen(f.Position)} matches expected chunk end.");
                    }

                }
                
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
