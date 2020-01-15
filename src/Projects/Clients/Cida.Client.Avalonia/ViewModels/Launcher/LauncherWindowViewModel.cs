﻿using System;
using Cida.Client.Avalonia.Api;
using Cida.Client.Avalonia.Services;
using Grpc.Core;
using ReactiveUI;

namespace Cida.Client.Avalonia.ViewModels.Launcher
{
    public class LauncherWindowViewModel : ViewModelBase
    {
        private readonly CidaConnectionService connectionService;
        private ViewModelBase content;
        private ConnectionScreenViewModel connection;
        private StatusScreenViewModel status;

        public event Action ConnectionSuccessfull;

        public ViewModelBase Content
        {
            get => content;
            set => this.RaiseAndSetIfChanged(ref content, value);
        }

        public LauncherWindowViewModel(CidaConnectionService connectionService)
        {
            this.connectionService = connectionService;
            this.connection = new ConnectionScreenViewModel();
            this.status = new StatusScreenViewModel();
            this.Content = connection;
        }

        public async void Connect()
        {
            if(await this.connectionService.Connect(this.connection.Address, 31564))
            {
                this.ConnectionSuccessfull?.Invoke();
            }
        }
    }
}