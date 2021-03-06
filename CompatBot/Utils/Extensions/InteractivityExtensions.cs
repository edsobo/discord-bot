﻿using System;
using System.Linq;
using System.Threading.Tasks;
using CompatBot.EventHandlers;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using Microsoft.Extensions.Caching.Memory;

namespace CompatBot.Utils
{
    public static class InteractivityExtensions
    {
        public static Task<(DiscordMessage? message, DiscordMessage? text, MessageReactionAddEventArgs? reaction)> WaitForMessageOrReactionAsync(
            this InteractivityExtension interactivity,
            DiscordMessage message,
            DiscordUser user,
            params DiscordEmoji?[] reactions)
            => WaitForMessageOrReactionAsync(interactivity, message, user, null, reactions);

        public static async Task<(DiscordMessage? message, DiscordMessage? text, MessageReactionAddEventArgs? reaction)> WaitForMessageOrReactionAsync(
            this InteractivityExtension interactivity,
            DiscordMessage message,
            DiscordUser user,
            TimeSpan? timeout,
            params DiscordEmoji?[] reactions)
        {
            if (reactions.Length == 0)
                throw new ArgumentException("At least one reaction must be specified", nameof(reactions));

            DiscordMessage? result = message;
            try
            {
                reactions = reactions.Where(r => r != null).ToArray();
                foreach (var emoji in reactions)
                    await message.ReactWithAsync(emoji!).ConfigureAwait(false);
                var expectedChannel = message.Channel;
                var waitTextResponseTask = interactivity.WaitForMessageAsync(m => m.Author == user && m.Channel == expectedChannel && !string.IsNullOrEmpty(m.Content), timeout);
                var waitReactionResponse = interactivity.WaitForReactionAsync(arg => reactions.Contains(arg.Emoji), message, user, timeout);
                await Task.WhenAny(
                    waitTextResponseTask,
                    waitReactionResponse
                ).ConfigureAwait(false);
                try
                {
                    await message.DeleteAllReactionsAsync().ConfigureAwait(false);
                }
                catch
                {
                    await message.DeleteAsync().ConfigureAwait(false);
                    result = null;
                }
                DiscordMessage? text = null;
                MessageReactionAddEventArgs? reaction = null;
                if (waitTextResponseTask.IsCompletedSuccessfully)
                    text = (await waitTextResponseTask).Result;
                if (waitReactionResponse.IsCompletedSuccessfully)
                    reaction = (await waitReactionResponse).Result;
                if (text != null)
                    try
                    {
                        DeletedMessagesMonitor.RemovedByBotCache.Set(text.Id, true, DeletedMessagesMonitor.CacheRetainTime);
                        await text.DeleteAsync().ConfigureAwait(false);
                    }
                    catch {}
                return (result, text, reaction);
            }
            catch (Exception e)
            {
                Config.Log.Warn(e, "Failed to get interactive reaction");
                return (result, null, null);
            }
        }
    }
}
