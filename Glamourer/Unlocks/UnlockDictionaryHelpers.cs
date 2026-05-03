using Dalamud.Interface.ImGuiNotification;
using Luna;

namespace Glamourer.Unlocks;

public static class UnlockDictionaryHelpers
{
    public const int Magic   = 0x00C0FFEE;
    public const int Version = 2;

    public static void Save(StreamWriter writer, IReadOnlyDictionary<uint, long> data)
    {
        // Not using by choice, as this would close the stream prematurely.
        var b = new BinaryWriter(writer.BaseStream);
        b.Write(Magic);
        b.Write(Version);
        b.Write(data.Count);
        foreach (var (id, timestamp) in data)
        {
            b.Write(id);
            b.Write(timestamp);
        }

        b.Flush();
    }

    public static int Load(string filePath, Dictionary<uint, long> data, Func<uint, bool> validate, string type)
    {
        data.Clear();
        if (!File.Exists(filePath))
            return -1;

        try
        {
            using var fileStream = File.OpenRead(filePath);
            using var b          = new BinaryReader(fileStream);
            var       magic      = b.ReadUInt32();
            bool      revertEndian;
            switch (magic)
            {
                case 0x00C0FFEE:
                    revertEndian = false;
                    break;
                case 0xEEFFC000:
                    revertEndian = true;
                    break;
                default:
                    Glamourer.Messager.NotificationMessage($"加载{type}解锁物品记录失败：文件头无效或文件已损坏。", NotificationType.Warning);
                    return -1;
            }

            var version = b.ReadInt32();
            var skips   = 0;
            var now     = DateTimeOffset.UtcNow;
            switch (version)
            {
                case 1:
                case Version:
                    var count = b.ReadInt32();
                    data.EnsureCapacity(count);
                    for (var i = 0; i < count; ++i)
                    {
                        var id        = b.ReadUInt32();
                        var timestamp = b.ReadInt64();
                        if (revertEndian)
                        {
                            id        = RevertEndianness(id);
                            timestamp = (long)RevertEndianness(timestamp);
                        }

                        var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
                        if (!validate(id)
                         || date < DateTimeOffset.UnixEpoch
                         || date > now
                         || !data.TryAdd(id, timestamp))
                            ++skips;
                    }

                    if (skips > 0)
                        Glamourer.Messager.NotificationMessage($"加载{type}解锁物品记录时，已跳过 {skips} 条无效或重复条目。",
                            NotificationType.Warning);

                    break;
                default:
                    Glamourer.Messager.NotificationMessage($"加载{type}解锁物品记录失败：不支持的文件版本（{version}）。",
                        NotificationType.Warning);
                    return version;
            }

            Glamourer.Log.Debug($"[UnlockManager] 已加载 {data.Count} 条{type}解锁物品记录。");
            return version;
        }
        catch (Exception ex)
        {
            Glamourer.Messager.NotificationMessage(ex, $"加载{type}解锁物品记录失败：发生未知错误。",
                $"加载{type}解锁物品记录失败：\n", NotificationType.Error);

            return -1;
        }
    }

    private static uint RevertEndianness(uint value)
        => ((value & 0x000000FFU) << 24) | ((value & 0x0000FF00U) << 8) | ((value & 0x00FF0000U) >> 8) | ((value & 0xFF000000U) >> 24);

    private static ulong RevertEndianness(long value)
        => (((ulong)value & 0x00000000000000FFU) << 56)
          | (((ulong)value & 0x000000000000FF00U) << 40)
          | (((ulong)value & 0x0000000000FF0000U) << 24)
          | (((ulong)value & 0x00000000FF000000U) << 8)
          | (((ulong)value & 0x000000FF00000000U) >> 8)
          | (((ulong)value & 0x0000FF0000000000U) >> 24)
          | (((ulong)value & 0x00FF000000000000U) >> 40)
          | (((ulong)value & 0xFF00000000000000U) >> 56);
}
