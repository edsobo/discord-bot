﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace CompatBot.Database.Providers
{
    using TSyscallStats = Dictionary<string, Dictionary<string, HashSet<string>>>;

    internal static class SyscallInfoProvider
    {
        private static readonly SemaphoreSlim Limiter = new SemaphoreSlim(1, 1);

        public static async Task SaveAsync(TSyscallStats syscallInfo)
        {
            if (syscallInfo == null || syscallInfo.Count == 0)
                return;

            if (await Limiter.WaitAsync(1000, Config.Cts.Token))
            {
                try
                {
                    using (var db = new ThumbnailDb())
                    {
                        foreach (var productCodeMap in syscallInfo)
                        {
                            var product = db.Thumbnail.AsNoTracking().FirstOrDefault(t => t.ProductCode == productCodeMap.Key)
                                          ?? db.Thumbnail.Add(new Thumbnail {ProductCode = productCodeMap.Key}).Entity;
                            foreach (var moduleMap in productCodeMap.Value)
                            foreach (var func in moduleMap.Value)
                            {
                                var syscall = db.SyscallInfo.AsNoTracking().FirstOrDefault(sci => sci.Module == moduleMap.Key && sci.Function == func)
                                              ?? db.SyscallInfo.Add(new SyscallInfo {Module = moduleMap.Key, Function = func}).Entity;
                                if (!db.SyscallToProductMap.Any(m => m.ProductId == product.Id && m.SyscallInfoId == syscall.Id))
                                    db.SyscallToProductMap.Add(new SyscallToProductMap {ProductId = product.Id, SyscallInfoId = syscall.Id});
                            }
                        }
                        await db.SaveChangesAsync(Config.Cts.Token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    Limiter.Release();
                }
            }
        }

        public static async Task<(int funcs, int links)> FixAsync()
        {
            int funcs = 0, links = 0;
            var syscallStats = new TSyscallStats();
            using (var db = new ThumbnailDb())
            {
                var funcsToFix = new List<SyscallInfo>(0);
                try
                {
                    funcsToFix = await db.SyscallInfo.Where(sci => sci.Function.Contains('(')).ToListAsync().ConfigureAwait(false);
                    funcs = funcsToFix.Count;
                    foreach (var sci in funcsToFix)
                    {
                        var productIds = await db.SyscallToProductMap.AsNoTracking().Where(m => m.SyscallInfoId == sci.Id).Select(m => m.Product.ProductCode).Distinct().ToListAsync().ConfigureAwait(false);
                        links += productIds.Count;
                        foreach (var productId in productIds)
                        {
                            if (!syscallStats.TryGetValue(productId, out var scInfo))
                                syscallStats[productId] = scInfo = new Dictionary<string, HashSet<string>>();
                            if (!scInfo.TryGetValue(sci.Module, out var smInfo))
                                scInfo[sci.Module] = smInfo = new HashSet<string>();
                            smInfo.Add(sci.Function.Split('(', 2)[0]);
                        }
                    }
                }
                catch (Exception e)
                {
                    Config.Log.Warn(e, "Failed to build fixed syscall mappings");
                    throw e;
                }
                await SaveAsync(syscallStats).ConfigureAwait(false);
                if (await Limiter.WaitAsync(1000, Config.Cts.Token))
                {
                    try
                    {
                        db.SyscallInfo.RemoveRange(funcsToFix);
                        await db.SaveChangesAsync().ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        Config.Log.Warn(e, "Failed to remove broken syscall mappings");
                        throw e;
                    }
                    finally
                    {
                        Limiter.Release();
                    }
                }
            }
            return (funcs, links);
        }
    }
}
