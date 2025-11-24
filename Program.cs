using SharpNBT;

namespace MCWorldInfo;

class Program
{
    public static long DirSize(DirectoryInfo pDir)
    {
        var fileInfos = pDir.GetFiles();
        var size      = fileInfos.Sum(pInfo => pInfo.Length);
        var dirs      = pDir.GetDirectories();
        return size + dirs.Sum(DirSize);
    }

    public static string FormatFileSize(long pBytes)
    {
        const int Unit = 1024;
        if (pBytes < Unit)
        {
            return $"{pBytes} B";
        }

        var exp = (int)(Math.Log(pBytes) / Math.Log(Unit));
        return $"{pBytes / Math.Pow(Unit, exp):F2} {"KMGTPE"[exp - 1]}B";
    }

    // static void PrintTag(Tag pTag, int pLevel)
    // {
    //     for (int i = 0; i < pLevel; i++)
    //     {
    //         Console.Write("--> ");
    //     }
    //
    //     Console.Write($"{pTag.Type} {pTag.Name}\n");
    //
    //     if (pTag.Type == TagType.Compound)
    //     {
    //         foreach (var tag in (CompoundTag)pTag)
    //         {
    //             PrintTag(tag, pLevel + 1);
    //         }
    //     }
    // }

    public class World
    {
        public enum WorldType
        {
            Vanilla,
            Bukkit,
            Forge,
            Unknown
        }

        public WorldType SaveType = WorldType.Vanilla;

        public struct WorldVersion
        {
            public int    Id;
            public String Name;
            public bool   Snapshot;

            public WorldVersion(CompoundTag pVersionTag)
            {
                Id       = pVersionTag.Get<IntTag>("Id");
                Name     = pVersionTag.Get<StringTag>("Name");
                Snapshot = pVersionTag.Get<ByteTag>("Snapshot").Bool;
            }

            public override string ToString()
            {
                return (Snapshot ? "Snapshot " : "") + $"{Name} (Id: {Id})";
            }
        }

        public WorldVersion Version;
        public string       Name;

        private CompoundTag mTagData;
        private CompoundTag mFml;

        public struct Mod
        {
            public string Id;
            public string Version;

            public Mod(CompoundTag pEntry)
            {
                Id      = pEntry.Get<StringTag>("ModId");
                Version = pEntry.Get<StringTag>("ModVersion");
            }
        }

        public List<Mod> Mods = [];

        public World(CompoundTag pNbtRoot, out bool pValid)
        {
            try
            {
                mTagData = pNbtRoot.Get<CompoundTag>("Data");
                Version  = new WorldVersion(mTagData.Get<CompoundTag>("Version"));
                Name     = mTagData.Get<StringTag>("LevelName");

                if (pNbtRoot.Count == 1)
                {
                    SaveType = WorldType.Vanilla;
                }

                if (pNbtRoot.TryGetValue("fml", out mFml))
                {
                    SaveType = WorldType.Forge;
                    foreach (var entry in mFml.Get<ListTag>("LoadingModList"))
                    {
                        Mods.Add(new Mod((CompoundTag)entry));
                    }
                }
                else if (pNbtRoot.TryGetValue("FML", out mFml))
                {
                    SaveType = WorldType.Forge;
                    foreach (var entry in mFml.Get<ListTag>("ModList"))
                    {
                        var mod = new Mod((CompoundTag)entry);

                        var ignored = new[]
                        {
                            "minecraft", "mcp", "FML", "forge"
                        };
                        var next = ignored.Any(pI => mod.Id.Trim() == pI);
                        if (next)
                        {
                            continue;
                        }

                        Mods.Add(mod);
                    }
                }
                else if (mTagData.ContainsKey("Bukkit.Version"))
                {
                    SaveType = WorldType.Bukkit;
                }
                else
                {
                    SaveType = WorldType.Unknown;
                }

                pValid = true;
            }
            catch (Exception e)
            {
                pValid = false;
            }
        }
    }

    public static List<string> DirSearch(string pDir, List<string> pFiles)
    {
        if (!Directory.Exists(pDir))
        {
            return [];
        }

        try
        {
            pFiles.AddRange(Directory.GetFiles(pDir, "*.*"));

            foreach (var d in Directory.GetDirectories(pDir))
            {
                DirSearch(d, pFiles);
            }

            return pFiles;
        }
        catch (Exception e)
        {
            return [];
        }
    }

    static void Main(string[] pArgs)
    {
        foreach (var file in DirSearch(pArgs[0], []))
        {
            if (Path.GetFileName(file) == "level.dat")
            {
                try
                {
                    var worldFolderInfo = Directory.GetParent(file);
                    var worldFolder     = worldFolderInfo.FullName;
                    var levelDat        = NbtFile.Read(Path.Combine(worldFolder, "level.dat"), FormatOptions.Java);

                    var world = new World(levelDat, out var valid);
                    if (!valid)
                    {
                        Console.WriteLine($"Invalid world {worldFolder}");
                        continue;
                    }

                    const int WIDTH = 40;

                    Console.WriteLine($"{worldFolder} ({world.Name}), Size: {FormatFileSize(DirSize(worldFolderInfo))}:");
                    Console.WriteLine("    Version:".PadRight(WIDTH) + world.Version);
                    Console.WriteLine("    Type:".PadRight(WIDTH) + world.SaveType);

                    if (world.SaveType == World.WorldType.Forge)
                    {
                        Console.WriteLine($"    Mods ({world.Mods.Count}):");
                        foreach (var mod in world.Mods)
                        {
                            Console.WriteLine($"        Mod ID '{mod.Id}'".PadRight(WIDTH) + mod.Version);
                        }
                    }

                    Console.WriteLine();
                }
                catch (Exception e)
                {

                }
            }
        }
    }
}