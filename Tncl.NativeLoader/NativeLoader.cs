using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Tncl.NativeLoader
{
    public class NativeLoader
    {
        private readonly Dictionary<string, IntPtr> _loadedLibrairies = new Dictionary<string, IntPtr>();
        private readonly NativeLoaderBase _loader;
        private readonly Platform _platform;
        private readonly ILogger _logger;

        public NativeLoaderWindowsOptions WindowsOptions { get; } = new NativeLoaderWindowsOptions();

        public NativeLoader() : this(null)
        {

        }

        public NativeLoader(ILogger logger)
        {
            _logger = logger;
            _logger?.LogDebug($"{nameof(NativeLoader)} - Detected platform: {RuntimeInformation.OSDescription}.");
            _platform = OSUtilities.GetOsPlatform();

            switch (_platform)
            {
                case Platform.Windows:
                    _loader = new NativeLoaderWindows(_logger, WindowsOptions);
                    break;
                case Platform.Linux:
                    _loader = new NativeLoaderUnix(_logger);
                    break;
                case Platform.OSX:
                    _loader = new NativeLoaderOSX(_logger);
                    break;
                default:
                    throw new NotImplementedException($"Loader not implemented for '{RuntimeInformation.OSDescription}'");
            }

            _logger?.LogDebug($"{nameof(NativeLoader)} initialization done.");
        }

        public IntPtr LoadLibrary(string name, string version)
        {
            return Load(name, version, null);
        }

        public IntPtr Load(LibraryItem library, ILibraryPathResolver libraryPathResolver = null)
        {
            if (library == null)
                throw new ArgumentNullException(nameof(library));

            var name = library.Name;
            var version = library.Version;
            var libraryVersion = library.OverrideLibraryName.FirstOrDefault(l => l.Platform == _platform);

            if (libraryVersion != null)
            {
                name = libraryVersion.Name;
                version = libraryVersion.Version;
            }

            return Load(name, version, libraryPathResolver);
        }

        public IntPtr Load(string name, string version = "", ILibraryPathResolver libraryPathResolver = null)
        {
            var handle = IntPtr.Zero;

            if (libraryPathResolver == null)
                libraryPathResolver = LibraryPathResolver.DefaultLibraryPathResolver;

            if (libraryPathResolver.FixupLibraryName)
                name = _loader.GetOSLibraryName(name, version);

            if (IsLibraryLoaded(name, version))
            {
                _logger?.LogDebug($"'{name}' already loaded.");
                return _loadedLibrairies[name];
            }

            var fileNames = libraryPathResolver.GetProbePaths(name);

            foreach (var fileName in fileNames)
            {
                handle = TryLoadLibrary(fileName);

                if (handle != IntPtr.Zero)
                    break;
            }

            if (handle == IntPtr.Zero)
                handle = _loader.Load(name);

            if (handle != IntPtr.Zero)
                _loadedLibrairies[name] = handle;
            else
                throw new DllNotFoundException($"Library '{name}' not found.");

            return _loadedLibrairies[name];
        }

        private IntPtr TryLoadLibrary(string fileName)
        {
            var result = _loader.Load(fileName);

            _logger?.LogDebug(result == IntPtr.Zero ? $"Failed to load '{fileName}'." : $"Loaded '{fileName}'.");

            return result;
        }

        public bool FreeLibrary(string fileName, string version = "")
        {
            fileName = _loader.GetOSLibraryName(fileName, version);

            if (!IsLibraryLoaded(fileName, version))
            {
                _logger?.LogDebug($"'{fileName}' was not loaded.");
                return false;
            }
            if (_loader.Free(_loadedLibrairies[fileName]))
            {
                _loadedLibrairies.Remove(fileName);
                _logger?.LogDebug($"FreeLibrary for '{fileName}' done.");
                return true;
            }
            else
            {
                _logger?.LogDebug($"FreeLibrary for '{fileName}' failed.");
                return false;
            }
        }

        public void FreeAll()
        {
            var keys = _loadedLibrairies.Keys.ToList();
            foreach (var key in keys)
            {
                if (_loader.Free(_loadedLibrairies[key]))
                {
                    _loadedLibrairies.Remove(key);
                }
            }
        }

        public IntPtr GetProcAddress(IntPtr handle, string name)
        {
            _logger?.LogDebug($"Trying to load native function '{name}'");

            var result = _loader.GetProcAddress(handle, name);

            _logger?.LogDebug(result == IntPtr.Zero ? $"Failed to load function '{name}'." : $"Loaded function '{name}'.");

            return result;
        }

        public T GetDelegateForFunctionName<T>(IntPtr handle, string name) where T : Delegate
        {
            var address = GetProcAddress(handle, name);
            if (address == IntPtr.Zero)
                throw new Exception($"{nameof(GetDelegateForFunctionName)} failed. handle: '{handle}', name: '{name}'");

            return (T)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
        }

        private bool IsLibraryLoaded(string fileName, string version)
        {
            fileName = _loader.GetOSLibraryName(fileName, version);

            return _loadedLibrairies.ContainsKey(fileName);
        }
    }
}