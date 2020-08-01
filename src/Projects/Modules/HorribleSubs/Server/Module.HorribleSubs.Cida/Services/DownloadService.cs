﻿using Filesystem = Cida.Api.Models.Filesystem;
using Google.Protobuf.WellKnownTypes;
using IrcClient;
using IrcClient.Downloaders;
using Microsoft.EntityFrameworkCore;
using Module.HorribleSubs.Cida.Models;
using Module.HorribleSubs.Cida.Models.Database;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cida.Api;
using System.Security.Cryptography;

namespace Module.HorribleSubs.Cida.Services
{
    public class DownloadService
    {
        public static string Separator = "/";   
        private static Filesystem.Directory DownloadedFilesDirectory = new Filesystem.Directory("Files", null);
        private readonly string tempFolder = Path.Combine(Path.GetTempPath(), "IrcDownloads");
        private readonly IrcClient.Clients.IrcClient ircClient;
        private readonly ConcurrentDictionary<string, CreateDownloaderContext> requestedDownloads;
        private readonly HorribleSubsDbContext context;
        private readonly IFtpClient ftpClient;

        public DownloadService(string host, int port, string connectionString, IFtpClient ftpClient)
        {
            string name = "ad_" + Guid.NewGuid();
            this.requestedDownloads = new ConcurrentDictionary<string, CreateDownloaderContext>();
            this.ircClient = new IrcClient.Clients.IrcClient(host, port, name, name, name, this.tempFolder);
            this.context = new HorribleSubsDbContext(connectionString);
            this.context.Database.EnsureCreated();

            this.ircClient.DownloadRequested += downloader =>
            {
                if(this.requestedDownloads.TryGetValue(downloader.Filename, out var context))
                {
                    context.Downloader = downloader;
                    context.ManualResetEvent.Set();
                }
            };
            this.ftpClient = ftpClient;
        }

        public async Task CreateDownloader(DownloadRequest downloadRequest)
        {
            if ((await this.context.Downloads.FindAsync(downloadRequest.FileName)) != null)
            {
                return;
            }

            if (!this.ircClient.IsConnected)
            {
                this.ircClient.Connect();
            }

            var createDownloaderContext = new CreateDownloaderContext()
            {
                Filename = downloadRequest.FileName,
            };
            var dccDownloaderTask = new Task<DccDownloader>(() =>
            {
                createDownloaderContext.ManualResetEvent.Wait();
                return createDownloaderContext.Downloader;

            }, TaskCreationOptions.LongRunning);

            if (this.requestedDownloads.TryAdd(downloadRequest.FileName, createDownloaderContext))
            {
                this.ircClient.SendMessage($"xdcc send #{downloadRequest.PackageNumber}", downloadRequest.BotName);
            }
            dccDownloaderTask.Start();
            var downloader = await dccDownloaderTask;

            await this.context.Downloads.AddAsync(new Download()
            {
                Name = downloader.Filename,
                Size = downloader.Filesize,
                DownloadStatus = DownloadStatus.Downloading,
            });

            await this.context.SaveChangesAsync();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () =>
            {
                await downloader.StartDownload();

                var file = new Filesystem.File(downloader.Filename, DownloadedFilesDirectory, new FileStream(Path.Combine(downloader.TempFolder, downloader.Filename), FileMode.Open, FileAccess.Read));
                var databaseDownloadEntry = await this.context.Downloads.FindAsync(downloader.Filename);
                if (databaseDownloadEntry != null)
                {
                    using var sha256 = SHA256.Create();
                    using var fileStream = await file.GetStreamAsync();
                    
                    databaseDownloadEntry.Sha256 = BitConverter.ToString(sha256.ComputeHash(fileStream)).Replace("-", "");
                    databaseDownloadEntry.Date = DateTime.Now;
                    databaseDownloadEntry.FtpPath = file.FullPath(Separator);

                    await this.ftpClient.UploadFileAsync(file);
                    databaseDownloadEntry.DownloadStatus = DownloadStatus.Available;
                    await this.context.SaveChangesAsync();
                }
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private class CreateDownloaderContext
        {
            public string Filename { get; set; }
            public DccDownloader Downloader { get; set; }
            public ManualResetEventSlim ManualResetEvent { get; } = new ManualResetEventSlim(false);
        }

    }
}
