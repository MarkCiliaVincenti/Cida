using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cida.Server.Api;

namespace Cida.Server.Module
{
    public class ModuleLoaderManager
    {
        public const string ModuleFolderName = "Modules";
        public const string ModuleFileExtension = "cidam";
        private readonly string moduleDirectory;
        private readonly IEnumerable<string> unpackedModuleDirectories;
        private readonly IGrpcRegistrar grpcRegistrar;
        private readonly List<CidaModule> modules;
        
        public ModuleLoaderManager(string moduleDirectory, IGrpcRegistrar grpcRegistrar, IEnumerable<string> unpackedModuleDirectories = null)
        {
            this.moduleDirectory = moduleDirectory;
            this.unpackedModuleDirectories = unpackedModuleDirectories ?? Array.Empty<string>();
            this.grpcRegistrar = grpcRegistrar;
            this.modules = new List<CidaModule>();

            if (!Directory.Exists(moduleDirectory))
            {
                Directory.CreateDirectory(moduleDirectory);
            }
        }

        public async Task LoadModulesAsync()
        {
            foreach (var modulePath in Directory.GetFiles(this.moduleDirectory, $"*.{ModuleFileExtension}"))
            {
                var module = CidaModule.Extract(modulePath);
                this.modules.Add(module);
                var loadedModule = module.Load();
                await this.grpcRegistrar.AddServicesAsync(loadedModule.GrpcServices);
            }

            foreach (var unpackedModuleDirectory in this.unpackedModuleDirectories)
            {
                var module = CidaModule.Unpacked(unpackedModuleDirectory);
                this.modules.Add(module);
                var loadedModule = module.Load();
                await this.grpcRegistrar.AddServicesAsync(loadedModule.GrpcServices);
            }
        }
    }
}