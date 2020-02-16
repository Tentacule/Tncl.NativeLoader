using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Tncl.NativeLoader
{
    internal static class EmitExtensions
    {

        internal static void CallMethod(this MethodBuilder methodBuilder, FieldInfo field, MethodInfo method, IList<ParameterInfo> parameters)
        {
            var ilGen = methodBuilder.GetILGenerator();

            ilGen.Emit(OpCodes.Ldarg_0); // this.
            ilGen.Emit(OpCodes.Ldfld, field);

            // Pass arguments
            for (var i = 0; i < parameters.Count(); i++)
            {
                switch (i)
                {
                    case 0:
                        ilGen.Emit(OpCodes.Ldarg_1);
                        break;
                    case 1:
                        ilGen.Emit(OpCodes.Ldarg_2);
                        break;
                    case 2:
                        ilGen.Emit(OpCodes.Ldarg_3);
                        break;
                    default:
                        ilGen.Emit(OpCodes.Ldarg, i + 1);
                        break;
                }
            }

            ilGen.Emit(OpCodes.Callvirt, method);
            ilGen.Emit(OpCodes.Ret);
        }

        internal static MethodBuilder AddMethod(
            this TypeBuilder typeBuilder, string name,
            MethodAttributes attributes, Type returnType, IList<ParameterInfo> infoArray)
        {
            var methodBuilder = typeBuilder.DefineMethod(name, attributes, returnType, infoArray.Select(p => p.ParameterType).ToArray());

            for (var parameterIndex = 0; parameterIndex < infoArray.Count; parameterIndex++)
            {
                var parameter = infoArray[parameterIndex];
                var parameterBuilder = methodBuilder.DefineParameter(
                    parameterIndex + 1,
                    parameter.Attributes,
                    parameter.Name);

                if (parameter.Attributes == ParameterAttributes.HasFieldMarshal)
                {
                    var constructorInfo =
                        typeof(MarshalAsAttribute).GetConstructor(
                            new[] { typeof(UnmanagedType) });

                    Debug.Assert(constructorInfo != null, nameof(constructorInfo) + " != null");

                    var customAttributeBuilder =
                        new CustomAttributeBuilder(
                            constructorInfo,
                            new object[] { GetAttributeUnmanagedType(parameter) });

                    parameterBuilder.SetCustomAttribute(customAttributeBuilder);
                }
            }

            return methodBuilder;
        }

        private static UnmanagedType GetAttributeUnmanagedType(ParameterInfo info)
        {
            UnmanagedType result = default;
            var marshalAsAttribute = info.GetCustomAttribute<MarshalAsAttribute>();

            if (marshalAsAttribute != null)
                result = marshalAsAttribute.Value;

            return result;
        }
    }
}
