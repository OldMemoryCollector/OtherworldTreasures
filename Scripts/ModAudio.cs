using System;
using System.Collections.Generic;
using Godot;
using STS2RitsuLib.Audio;

namespace OtherworldTreasures.Scripts;

/// <summary>
/// 模组自定义音效。
///
/// 音频文件放在 OtherworldTreasures/audio/ 下（Godot 支持的 .wav / .ogg / .mp3），
/// 随模组 pck 打包；播放走 RitsuLib 的音频服务，和原版一样受游戏音量设置控制。
/// 原版 FMOD 事件（event:/...）只能复用、不能新增，所以自定义音效只能走这条路。
/// </summary>
internal static class ModAudio
{
    // 找不到文件的路径只报一次，避免每次触发都刷日志
    private static readonly HashSet<string> s_missingReported = new();

    internal static void PlayOneShot(string resourcePath, float volume = 1f)
    {
        try
        {
            if (!ResourceLoader.Exists(resourcePath))
            {
                if (s_missingReported.Add(resourcePath))
                {
                    Entry.Logger.Warn($"[ModAudio] 找不到音频 {resourcePath}，已跳过播放");
                }
                return;
            }

            GameAudioService.Shared.PlayOneShot(
                AudioSource.ResourceFile(resourcePath),
                new AudioPlaybackOptions { Volume = volume, Pitch = 1f });
        }
        catch (Exception e)
        {
            // 音效失败绝不能影响玩法流程
            Entry.Logger.Warn($"[ModAudio] 播放 {resourcePath} 失败：{e.Message}");
        }
    }
}
