using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace AmbientOS.VisualStudio
{
    static class Hooking
    {
        private static ModuleBuilder dynamicModule = CreateModule();
        private static long dynamicTypeCount = 0;

        private static ModuleBuilder CreateModule()
        {
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("DynamicAssembly"), AssemblyBuilderAccess.Run);
            return assemblyBuilder.DefineDynamicModule("DynamicModule");
        }

        static TypeBuilder CreateType()
        {
            var typeID = Interlocked.Increment(ref dynamicTypeCount);
            return dynamicModule.DefineType("DynamicType_" + typeID,
                TypeAttributes.Public
                | TypeAttributes.Class
                | TypeAttributes.AutoClass
                | TypeAttributes.AnsiClass
                | TypeAttributes.ExplicitLayout);
        }

        /// <summary>
        /// Emits IL code to put the specified object on top of the evaluation stack.
        /// </summary>
        public static void LdObj(this ILGenerator il, object obj)
        {
            var typeBuilder = CreateType();
            typeBuilder.DefineField("value", obj.GetType(), FieldAttributes.Static | FieldAttributes.Public);
            var type = typeBuilder.CreateType();

            var field = type.GetField("value", BindingFlags.Static | BindingFlags.Public);
            field.SetValue(null, obj);

            il.Emit(OpCodes.Ldsfld, field);
        }

        /// <summary>
        /// Emits IL code to invoke the specified delegate.
        /// </summary>
        /// <param name="del">The delegate to be invoked.</param>
        /// <param name="emitArgs">An action that emits code on the IL generator to put the call arguments on the evaluation stack.</param>
        public static void EmitCall(this ILGenerator il, Delegate del, Action emitArgs)
        {
            il.LdObj(del);

            emitArgs();

            var methodInfo = del.GetType().GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);
            il.Emit(OpCodes.Callvirt, methodInfo);
        }

        /// <summary>
        /// Returns the runtime method handle of a method that may be a dynamic method.
        /// Special measures are neccessary since DynamicMethod does not implement the MethodHandle property.
        /// </summary>
        static RuntimeMethodHandle GetMethodHandle(this MethodInfo methodInfo)
        {
            if (methodInfo is DynamicMethod) {
                var getMethodDescriptor = typeof(DynamicMethod).GetMethod("GetMethodDescriptor", BindingFlags.Instance | BindingFlags.NonPublic);
                return (RuntimeMethodHandle)getMethodDescriptor.Invoke(methodInfo, new object[0]);
            }
            return methodInfo.MethodHandle;
        }

        /// <summary>
        /// Generates a dynamic method that wraps the provided delegate.
        /// </summary>
        public static DynamicMethod ToDynamicMethod(this Delegate del)
        {
            var dynamicMethod = new DynamicMethod("",
                (MethodAttributes.Public | MethodAttributes.Static) /* & del.Method.Attributes */,
                (CallingConventions.Standard) & del.Method.CallingConvention,
                del.Method.ReturnType,
                del.Method.GetParameters().Select(p => p.ParameterType).ToArray(),
                del.Method.Module,
                true);

            var il = dynamicMethod.GetILGenerator();

            il.EmitCall(del, () => {
                var argCount = del.Method.GetParameters().Count();

                if (argCount > 0) il.Emit(OpCodes.Ldarg_0);
                if (argCount > 1) il.Emit(OpCodes.Ldarg_1);
                if (argCount > 2) il.Emit(OpCodes.Ldarg_2);
                if (argCount > 3) il.Emit(OpCodes.Ldarg_3);
                if (argCount > 4) throw new NotImplementedException();
            });
            il.Emit(OpCodes.Ret);

            return dynamicMethod;
        }

        /// <summary>
        /// Replaces a method by the specified delegate. The delegate must have the same signature as the method to be replaced.
        /// You must do this before any referencing methods are prepared by the just-in-time compiler, e.g. before the calling functions are executed for the first time.
        /// Make sure that you test the effectiveness at both Debug and Release configuration, as the JIT behaves differently.
        /// </summary>
        /// <returns>A delegate that represents the old method. The delegate type will be equal to the type of the replacement method.</returns>
        public static DynamicMethod Replace(MethodInfo methodToReplace, Delegate replacement)
        {
            // we wrap the delegate by a dynamically compiled stub code, otherwise there will be problems with the stack layout
            var dynamicMethod = replacement.ToDynamicMethod();
            Swap(methodToReplace, dynamicMethod);
            //Swap(dynamicMethod.GetMethodHandle(), old.Method.MethodHandle);
            //return dynamicMethod.CreateDelegate(replacement.GetType());
            return dynamicMethod;
        }

        /// <summary>
        /// Swaps the native code pointer of two methods. The two methods are validated to have the a compatible signature.
        /// </summary>
        public static void Swap(MethodInfo method1, MethodInfo method2)
        {
            if (method1.ReturnType != method2.ReturnType)
                throw new ArgumentException("The methods being swapped must have the same return types.");

            var args1 = method1.GetParameters().Select(p => p.ParameterType).ToArray();
            var args2 = method2.GetParameters().Select(p => p.ParameterType).ToArray();

            var thisType1 = method1.CallingConvention.HasFlag(CallingConventions.HasThis) ? method1.DeclaringType : args1.FirstOrDefault();
            var thisType2 = method2.CallingConvention.HasFlag(CallingConventions.HasThis) ? method2.DeclaringType : args2.FirstOrDefault();

            bool canAssign1To2 = true;
            bool canAssign2To1 = true;

            if (method1.CallingConvention.HasFlag(CallingConventions.HasThis) || method2.CallingConvention.HasFlag(CallingConventions.HasThis)) {
                if (thisType1 == null || thisType2 == null)
                    throw new ArgumentException("Either both or none of the swapped methods must take an instance argument.");
                if (!(canAssign2To1 = thisType1.IsAssignableFrom(thisType2)) && !(canAssign1To2 = thisType2.IsAssignableFrom(thisType1)))
                    throw new ArgumentException("The methods must have compatible instance arguments.");

                if (!method1.CallingConvention.HasFlag(CallingConventions.HasThis))
                    args1 = args1.Skip(1).ToArray();
                if (!method2.CallingConvention.HasFlag(CallingConventions.HasThis))
                    args2 = args2.Skip(1).ToArray();
            }

            canAssign1To2 &= args2.Select((arg, i) => arg.IsAssignableFrom(args1[i])).All(b => b);
            canAssign2To1 &= args1.Select((arg, i) => arg.IsAssignableFrom(args2[i])).All(b => b);

            if (!canAssign1To2 && !canAssign2To1)
                throw new ArgumentException("Either of the two methods must have arguments that are directly assignable to the other method.");

            Swap(method1.GetMethodHandle(), method2.GetMethodHandle());
        }

        /// <summary>
        /// Swaps the native code pointers of two methods.
        /// If the two methods are incompatible (e.g. in terms argument types), a subsequent invokation may throw a verification exception.
        /// </summary>
        /// <remarks>
        /// This works for both x86 and AMD64 configurations and should work for other architectures as well.
        /// </remarks>
        public static void Swap(RuntimeMethodHandle method1, RuntimeMethodHandle method2)
        {
            RuntimeHelpers.PrepareMethod(method1);
            RuntimeHelpers.PrepareMethod(method2);

            var nativePointer1 = method1.GetFunctionPointer();
            var nativePointer2 = method2.GetFunctionPointer();

            var nativePointer1Address = method1.Value + 8;
            var nativePointer2Address = method2.Value + 8;

            unsafe
            {
                *((IntPtr*)nativePointer1Address.ToPointer()) = nativePointer2;
                *((IntPtr*)nativePointer2Address.ToPointer()) = nativePointer1;
            }
        }

        /// <summary>
        /// Sometimes, the function pointer of a method points to a JMP instruction, which then jumps to the actual function block.
        /// </summary>
        public static IntPtr GetActualFunctionPointer(this MethodInfo method)
        {
            var handle = method.GetMethodHandle();
            RuntimeHelpers.PrepareMethod(handle);
            var address = handle.GetFunctionPointer();

            byte jmpCode;
            var arch = method.Module.Assembly.GetName().ProcessorArchitecture;
            switch (arch) {
                case ProcessorArchitecture.MSIL: jmpCode = 0xE9; break; // this happens if we compile for AnyCPU, which is kind of stupid
                case ProcessorArchitecture.X86: jmpCode = 0xE9; break;
                case ProcessorArchitecture.Amd64: jmpCode = 0xE9; break;
                default: throw new Exception("Unknown OpCodes for the current CPU architecture");
            }

            if (Marshal.ReadByte(address, 0) == jmpCode)
                address += Marshal.ReadInt32(address, 1); // the JMP displacement is signed 32-bit, for both x86 and AMD64

            return address;
        }

        /// <summary>
        /// Extracts the native code of the specified method.
        /// </summary>
        public static byte[] ExtractCode(MethodInfo method, out IntPtr address, int length = -1)
        {
            address = method.GetActualFunctionPointer();

            byte returnCode0, returnCode16;
            var arch = method.Module.Assembly.GetName().ProcessorArchitecture;
            switch (arch) {
                case ProcessorArchitecture.MSIL: returnCode0 = 0xC3; returnCode16 = 0xC2; break; // this happens if we compile for AnyCPU, which is kind of stupid
                case ProcessorArchitecture.X86: returnCode0 = 0xC3; returnCode16 = 0xC2; break;
                case ProcessorArchitecture.Amd64: returnCode0 = 0xC3; returnCode16 = 0xC2; break;
                default: throw new Exception("Unknown OpCodes for the current CPU architecture");
            }

            // try to determine code length heuristically
            if (length < 0) {
                for (length = 0; Marshal.ReadByte(address, length) != returnCode0 && Marshal.ReadByte(address, length) != returnCode16; length++) ;
                if (Marshal.ReadByte(address, length) == returnCode0)
                    length += 1;
                else if (Marshal.ReadByte(address, length) == returnCode16)
                    length += 3;
            }

            var result = new byte[length];
            Marshal.Copy(address, result, 0, length);
            return result;
        }

        /// <summary>
        /// Overrides the native code of the specified method with the native code from another method.
        /// Do not try this at home. You will most likely destabilize the runtime if you don't take care,
        /// for instance if the replacement code is longer than the old code or if it has a different signature.
        /// </summary>
        public static void Override(MethodInfo methodToReplace, MethodInfo replacementMethod)
        {
            IntPtr address;
            Override(methodToReplace, ExtractCode(replacementMethod, out address));
        }

        /// <summary>
        /// Overrides the native code of the specified method with the provided code.
        /// Do not try this at home. You will most likely destabilize the runtime if you don't take care,
        /// for instance if the replacement code is longer than the old code or if it has a different signature.
        /// </summary>
        public static void Override(MethodInfo method, byte[] code)
        {
            Marshal.Copy(code, 0, method.GetActualFunctionPointer(), code.Count());
        }
    }
}
