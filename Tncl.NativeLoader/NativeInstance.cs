using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Tncl.NativeLoader
{
    public static class NativeInstance
    {
        private static readonly Platform Platform = OSUtilities.GetOsPlatform();

        public static T CreateInstance<T>(this global::Tncl.NativeLoader.NativeLoader loader) where T : class
        {
            var interfaceType = typeof(T);
            if (!typeof(T).IsInterface && !interfaceType.IsPublic)
                throw new Exception($"{interfaceType.Name} is not a public interface.");

            var assemblyName = $"Assembly.{interfaceType.Name}";
            var typeName = $"{assemblyName}.Type";

            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName);
            var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public, typeof(object), new[] { interfaceType });

            var interfaceMethods = GetInterfaceMethodDefinitions(interfaceType, moduleBuilder, typeBuilder);

            typeBuilder.DefineConstructor(interfaceMethods);

            var implementationType = typeBuilder.CreateTypeInfo();
            return (T)Activator.CreateInstance(implementationType, loader);
        }

        private static IList<InterfaceMethodDefinition> GetInterfaceMethodDefinitions(Type interfaceType, ModuleBuilder moduleBuilder, TypeBuilder typeBuilder)
        {
            var interfaceMethods = interfaceType.GetMethods();
            var methods = new List<InterfaceMethodDefinition>();

            foreach (var interfaceMethod in interfaceMethods)
            {
                var method = new InterfaceMethodDefinition { InterfaceMethodInfo = interfaceMethod };
                var unmanagedFunctionPointerAttribute = GetUnmanagedFunctionPointerAttribute(interfaceMethod);
                method.UnmanagedFunctionPointerAttribute = unmanagedFunctionPointerAttribute;

                method.LibraryName = unmanagedFunctionPointerAttribute.LibraryName;
                method.LibraryVersion = unmanagedFunctionPointerAttribute.LibraryVersion;

                // Use overrides if any.
                var nativeLoaderOverrideAttribute = GetNativeLoaderOverrideAttribute(interfaceMethod);
                if (nativeLoaderOverrideAttribute != null)
                {
                    method.LibraryName = nativeLoaderOverrideAttribute.LibraryName;
                    method.LibraryVersion = nativeLoaderOverrideAttribute.LibraryVersion;
                }

                method.DelegateType = moduleBuilder.CreateDelegateType(method);
                method.DelegateField = typeBuilder.DefineField($"_{method.InterfaceMethodInfo.Name}Delegate", method.DelegateType, FieldAttributes.Private);

                typeBuilder.DefineMethodOverrideForDelegateMethod(method);

                methods.Add(method);
            }

            return methods;
        }

        #region Delegate

        private static Type CreateDelegateType(this ModuleBuilder moduleBuilder, InterfaceMethodDefinition interfaceMethodDefinition)
        {
            var delegateName = $"{interfaceMethodDefinition.InterfaceMethodInfo.Name}Delegate";
            var delegateBuilder = moduleBuilder.DefineType(
                delegateName,
                TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.Sealed,
                typeof(MulticastDelegate));

            delegateBuilder.AddUnmanagedFunctionPointerAttribute(interfaceMethodDefinition.UnmanagedFunctionPointerAttribute);

            // ctor
            var ctorBuilder = delegateBuilder.DefineConstructor(
                MethodAttributes.Public | MethodAttributes.HideBySig |
                MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, CallingConventions.Standard,
                new[] { typeof(object), typeof(IntPtr) });

            ctorBuilder.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);
            ctorBuilder.DefineParameter(1, ParameterAttributes.HasDefault, "object");
            ctorBuilder.DefineParameter(2, ParameterAttributes.HasDefault, "method");

            // Implement Invoke()
            var methodBuilder = delegateBuilder.AddMethod(
                "Invoke",
                MethodAttributes.Public,
                interfaceMethodDefinition.InterfaceMethodInfo.ReturnType,
                interfaceMethodDefinition.InterfaceMethodInfo.GetParameters());

            methodBuilder.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

            return delegateBuilder.CreateTypeInfo();
        }

        private static void AddUnmanagedFunctionPointerAttribute(this TypeBuilder typeBuilder, RuntimeUnmanagedFunctionPointerAttribute importAttribute)
        {
            var attributeConstructor = typeof(UnmanagedFunctionPointerAttribute).GetConstructor(new[] { typeof(CallingConvention) });
            var unmanagedFunctionPointerAttributeType = typeof(UnmanagedFunctionPointerAttribute);

            Debug.Assert(attributeConstructor != null, nameof(attributeConstructor) + " != null");

            var nameFields = (
                from name in new[] { "CharSet", "BestFitMapping", "ThrowOnUnmappableChar", "SetLastError" }
                select unmanagedFunctionPointerAttributeType.GetField(name)).ToArray();

            var fieldValues = new object[]
            {
                importAttribute.CharSet, importAttribute.BestFitMapping, importAttribute.ThrowOnUnmappableChar,
                importAttribute.SetLastError
            };

            var attributeBuilder = new CustomAttributeBuilder(
                attributeConstructor,
                new object[] { importAttribute.CallingConvention },
                nameFields, fieldValues);

            typeBuilder.SetCustomAttribute(attributeBuilder);
        }

        private static void DefineMethodOverrideForDelegateMethod(this TypeBuilder typeBuilder, InterfaceMethodDefinition interfaceMethodDefinition)
        {
            var delegateInvokeMethod = interfaceMethodDefinition.DelegateType.GetMethod("Invoke");
            var parameters = interfaceMethodDefinition.InterfaceMethodInfo.GetParameters();
            var methodBuilder =
                typeBuilder.AddMethod(
                    interfaceMethodDefinition.InterfaceMethodInfo.Name,
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    interfaceMethodDefinition.InterfaceMethodInfo.ReturnType,
                    parameters);

            methodBuilder.CallMethod(interfaceMethodDefinition.DelegateField, delegateInvokeMethod, parameters);

            typeBuilder.DefineMethodOverride(methodBuilder, interfaceMethodDefinition.InterfaceMethodInfo);
        }

        #endregion

        #region Constructor

        private static void DefineConstructor(this TypeBuilder typeBuilder, IList<InterfaceMethodDefinition> methods)
        {
            var constructorBuilder = typeBuilder.DefineConstructor(
                 MethodAttributes.Public,
                 CallingConventions.Standard,
                 new[] { typeof(global::Tncl.NativeLoader.NativeLoader) });
            constructorBuilder.DefineParameter(1, ParameterAttributes.HasDefault, "loader");

            var ilGen = constructorBuilder.GetILGenerator();

            // Library handles + temp method handle
            var libraries = GetLibrairies(methods);
            for (var i = 0; i < libraries.Count; i++)
                ilGen.DeclareLocal(typeof(IntPtr));

            ilGen.DeclareLocal(typeof(IntPtr));

            ilGen.LoadLibrairies(libraries);

            foreach (var method in methods)
            {
                ilGen.SetDelegateField(method, libraries);
            }

            ilGen.Emit(OpCodes.Ret);
        }

        private static SortedList<string, LibraryVersion> GetLibrairies(IList<InterfaceMethodDefinition> methods)
        {
            var result = new SortedList<string, LibraryVersion>();
            foreach (var method in from method in methods
                                   where !result.ContainsKey(method.LibraryName)
                                   select method)
            {
                result.Add(method.LibraryName, new LibraryVersion() { Name = method.LibraryName, Version = method.LibraryVersion });
            }

            return result;
        }

        private static void LoadLibrairies(this ILGenerator ilGen, SortedList<string, LibraryVersion> libraries)
        {
            var loadLibraryMethod = typeof(global::Tncl.NativeLoader.NativeLoader).GetMethod(nameof(global::Tncl.NativeLoader.NativeLoader.LoadLibrary));
            if (loadLibraryMethod == null)
                throw new Exception($"{nameof(global::Tncl.NativeLoader.NativeLoader)}.{nameof(global::Tncl.NativeLoader.NativeLoader.LoadLibrary)} method not found.");

            // Call LoadLibrary and store handles in local variables.
            for (var i = 0; i < libraries.Count; i++)
            {
                // Preparing
                var library = libraries.ElementAt(i).Value.Name;
                var version = libraries.ElementAt(i).Value.Version;
                // Load LibraryLoader
                ilGen.Emit(OpCodes.Ldarg_1);

                // Load arguments: libraryName/version (or null if not specified)
                ilGen.Emit(OpCodes.Ldstr, library);
                if (string.IsNullOrEmpty(version))
                {
                    ilGen.Emit(OpCodes.Ldnull);
                }
                else
                {
                    ilGen.Emit(OpCodes.Ldstr, version);
                }

                ilGen.Emit(OpCodes.Callvirt, loadLibraryMethod);
                // Store libraryHandle in local variables [i]
                ilGen.Emit(OpCodes.Stloc, i);
            }
        }

        private static void SetDelegateField(this ILGenerator ilGen, InterfaceMethodDefinition method, SortedList<string, LibraryVersion> libraries)
        {
            var getProcAddressMethod = typeof(global::Tncl.NativeLoader.NativeLoader).GetMethod(nameof(global::Tncl.NativeLoader.NativeLoader.GetProcAddress));
            var getTypeFromHandleMethod = typeof(Type).GetMethod("GetTypeFromHandle");
            var getDelegateForFunctionPointerMethod = typeof(Marshal).GetMethod("GetDelegateForFunctionPointer", new[] { typeof(IntPtr), typeof(Type) });

            if (getProcAddressMethod == null)
                throw new Exception($"{nameof(global::Tncl.NativeLoader.NativeLoader)}.{nameof(global::Tncl.NativeLoader.NativeLoader.GetProcAddress)} method not found.");
            if (getTypeFromHandleMethod == null)
                throw new Exception("getTypeFromHandleMethod method not found.");
            if (getDelegateForFunctionPointerMethod == null)
                throw new Exception("GetDelegateForFunctionPointer method not found.");

            var tempMethodHandleIndex = libraries.Count;

            // Preparing
            var libraryIndex = libraries.IndexOfKey(method.LibraryName);
            var methodName = method.UnmanagedFunctionPointerAttribute.EntryPoint ?? method.InterfaceMethodInfo.Name;

            // Call GetProcAddress() then Marshal.GetDelegateForFunctionPointer() and store the casted result in delegate field.

            // Load Library Loader
            ilGen.Emit(OpCodes.Ldarg_1);
            // Load libraryHandle from local variables
            ilGen.Emit(OpCodes.Ldloc, libraryIndex);
            // Load methodName
            ilGen.Emit(OpCodes.Ldstr, methodName);
            // Call NativeLoader.GetProcAddress(libraryHandle, methodName)
            ilGen.Emit(OpCodes.Callvirt, getProcAddressMethod);
            // Store methodHandle in local variables
            ilGen.Emit(OpCodes.Stloc, tempMethodHandleIndex);

            // this.
            ilGen.Emit(OpCodes.Ldarg_0);
            // Load methodHandle from local variables
            ilGen.Emit(OpCodes.Ldloc_1);
            // Load methodDelegate token
            ilGen.Emit(OpCodes.Ldtoken, method.DelegateType);
            // Call typeof(methodDelegate)                                
            ilGen.Emit(OpCodes.Call, getTypeFromHandleMethod);
            // Call Marshal.GetDelegateForFunctionPointer(methodHandle, typeof(methodDelegate))
            ilGen.Emit(OpCodes.Call, getDelegateForFunctionPointerMethod);
            ilGen.Emit(OpCodes.Castclass, method.DelegateType); // Cast to delegate type
            ilGen.Emit(OpCodes.Stfld, method.DelegateField); // Store in delegate field
        }

        #endregion

        private static RuntimeUnmanagedFunctionPointerAttribute GetUnmanagedFunctionPointerAttribute(MemberInfo methodInfo)
        {
            var attribute =
                methodInfo.GetCustomAttributes<RuntimeUnmanagedFunctionPointerAttribute>(true)
                    .FirstOrDefault();

            if (attribute == null)
                throw new Exception($"{nameof(RuntimeUnmanagedFunctionPointerAttribute)} not set for method '{methodInfo.Name}'.");

            return attribute;
        }

        private static NativeLoaderOverrideAttribute GetNativeLoaderOverrideAttribute(MemberInfo methodInfo)
        {
            var attribute = methodInfo.GetCustomAttributes<NativeLoaderOverrideAttribute>(true)
                .FirstOrDefault(a => a.Platform == Platform);

            return attribute;
        }

        private class InterfaceMethodDefinition
        {
            public MethodInfo InterfaceMethodInfo { get; set; }
            public Type DelegateType { get; set; }
            public FieldInfo DelegateField { get; set; }
            public RuntimeUnmanagedFunctionPointerAttribute UnmanagedFunctionPointerAttribute { get; set; }
            public string LibraryName { get; set; }
            public string LibraryVersion { get; set; }
        }

    }
}