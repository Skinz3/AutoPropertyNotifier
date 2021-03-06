using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace AutoPropertyNotifier
{
    public class INotifyPropertyChangedProxyTypeGenerator
    {
        private static AssemblyBuilder _ab;
        private static ModuleBuilder _mb;

        private static MethodBuilder CreateAddRemoveMethod(
           TypeBuilder typeBuilder, FieldBuilder eventField, bool isAdd)
        {
            string prefix = "remove_";
            string delegateAction = "Remove";
            if (isAdd)
            {
                prefix = "add_";
                delegateAction = "Combine";
            }
            MethodBuilder addremoveMethod =
            typeBuilder.DefineMethod(prefix + "PropertyChanged",
               MethodAttributes.Public |
               MethodAttributes.SpecialName |
               MethodAttributes.NewSlot |
               MethodAttributes.HideBySig |
               MethodAttributes.Virtual |
               MethodAttributes.Final,
               null,
               new[] { typeof(PropertyChangedEventHandler) });
            MethodImplAttributes eventMethodFlags =
                MethodImplAttributes.Managed |
                MethodImplAttributes.Synchronized;
            addremoveMethod.SetImplementationFlags(eventMethodFlags);

            ILGenerator ilGen = addremoveMethod.GetILGenerator();

            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldfld, eventField);
            ilGen.Emit(OpCodes.Ldarg_1);
            ilGen.EmitCall(OpCodes.Call,
                typeof(Delegate).GetMethod(
                delegateAction,
                new[] { typeof(Delegate), typeof(Delegate) }),
                null);
            ilGen.Emit(OpCodes.Castclass, typeof(
            PropertyChangedEventHandler));
            ilGen.Emit(OpCodes.Stfld, eventField);
            ilGen.Emit(OpCodes.Ret);

            MethodInfo intAddRemoveMethod =
            typeof(INotifyPropertyChanged).GetMethod(
            prefix + "PropertyChanged");
            typeBuilder.DefineMethodOverride(
            addremoveMethod, intAddRemoveMethod);

            return addremoveMethod;
        }
        private static FieldBuilder CreatePropertyChangedEvent(
TypeBuilder typeBuilder)
        {
            // public event PropertyChangedEventHandler PropertyChanged;

            FieldBuilder eventField =
                typeBuilder.DefineField("PropertyChanged",
                typeof(PropertyChangedEventHandler),
                FieldAttributes.Private);
            EventBuilder eventBuilder =
                typeBuilder.DefineEvent(
                "PropertyChanged",
                EventAttributes.None,
                typeof(PropertyChangedEventHandler));

            eventBuilder.SetAddOnMethod(
            CreateAddRemoveMethod(typeBuilder, eventField, true));
            eventBuilder.SetRemoveOnMethod(
            CreateAddRemoveMethod(typeBuilder, eventField, false));

            return eventField;
        }
        public static Type GenerateProxy(Type type)
        {
            if (_ab == null)
            {
                var assmName = new AssemblyName("DynamicAssembly");
                _ab = AssemblyBuilder.DefineDynamicAssembly(
                             assmName,
                             AssemblyBuilderAccess.Run);
                _mb = _ab.DefineDynamicModule(assmName.Name);
            }

            // public class [TypeName]__proxy
            //    : [TypeName], INotifyPropertyChanged
            TypeBuilder typeBuilder = _mb.DefineType(
                type.Name + "__proxy", TypeAttributes.Public, type);
            typeBuilder.AddInterfaceImplementation(
            typeof(INotifyPropertyChanged));

            FieldBuilder eventField =
            CreatePropertyChangedEvent(typeBuilder);

            MethodBuilder raisePropertyChanged =
            CreateRaisePropertyChanged(typeBuilder, eventField);

            // get all the public or protected
            // virtual property setters.
            var props = from p in
                            type.GetProperties(
                                BindingFlags.Public |
                                BindingFlags.NonPublic |
                                BindingFlags.Instance |
                                BindingFlags.FlattenHierarchy)
                        where
                       p.GetSetMethod().IsVirtual && (p.GetSetMethod().IsPublic ||
                        p.GetSetMethod().IsFamily)
                        select p;
            props.ToList().ForEach(
            (item) => WrapMethod(
            item, raisePropertyChanged, typeBuilder));

            Type ret = typeBuilder.CreateType();
            return ret;
        }

        private static MethodBuilder CreateRaisePropertyChanged(
TypeBuilder typeBuilder, FieldBuilder eventField)
        {
            MethodBuilder raisePropertyChangedBuilder =
                typeBuilder.DefineMethod(
                "RaisePropertyChanged",
                MethodAttributes.Family | MethodAttributes.Virtual,
                null, new Type[] { typeof(string) });

            ILGenerator raisePropertyChangedIl =
            raisePropertyChangedBuilder.GetILGenerator();
            Label labelExit = raisePropertyChangedIl.DefineLabel();

            // if (PropertyChanged == null)
            // {
            //      return;
            // }
            raisePropertyChangedIl.Emit(OpCodes.Ldarg_0);
            raisePropertyChangedIl.Emit(OpCodes.Ldfld, eventField);
            raisePropertyChangedIl.Emit(OpCodes.Ldnull);
            raisePropertyChangedIl.Emit(OpCodes.Ceq);
            raisePropertyChangedIl.Emit(OpCodes.Brtrue, labelExit);

            // this.PropertyChanged(this,
            // new PropertyChangedEventArgs(propertyName));
            raisePropertyChangedIl.Emit(OpCodes.Ldarg_0);
            raisePropertyChangedIl.Emit(OpCodes.Ldfld, eventField);
            raisePropertyChangedIl.Emit(OpCodes.Ldarg_0);
            raisePropertyChangedIl.Emit(OpCodes.Ldarg_1);
            raisePropertyChangedIl.Emit(OpCodes.Newobj,
                typeof(PropertyChangedEventArgs)
                .GetConstructor(new[] { typeof(string) }));
            raisePropertyChangedIl.EmitCall(OpCodes.Callvirt,
                typeof(PropertyChangedEventHandler)
                .GetMethod("Invoke"), null);

            // return;
            raisePropertyChangedIl.MarkLabel(labelExit);
            raisePropertyChangedIl.Emit(OpCodes.Ret);

            return raisePropertyChangedBuilder;
        }
        private static void WrapMethod(PropertyInfo item,
MethodBuilder raisePropertyChanged, TypeBuilder typeBuilder)
        {
            MethodInfo setMethod = item.GetSetMethod();

            //get an array of the parameter types.
            var types = from t in setMethod.GetParameters()
                        select t.ParameterType;

            MethodBuilder setMethodBuilder = typeBuilder.DefineMethod(
                setMethod.Name, setMethod.Attributes,
                setMethod.ReturnType, types.ToArray());
            typeBuilder.DefineMethodOverride(
            setMethodBuilder, setMethod);
            ILGenerator setMethodWrapperIl =
                setMethodBuilder.GetILGenerator();

            // base.[PropertyName] = value;
            setMethodWrapperIl.Emit(OpCodes.Ldarg_0);
            setMethodWrapperIl.Emit(OpCodes.Ldarg_1);
            setMethodWrapperIl.EmitCall(
            OpCodes.Call, setMethod, null);

            // RaisePropertyChanged("[PropertyName]");
            setMethodWrapperIl.Emit(OpCodes.Ldarg_0);
            setMethodWrapperIl.Emit(OpCodes.Ldstr, item.Name);
            setMethodWrapperIl.EmitCall(
            OpCodes.Call, raisePropertyChanged, null);

            // return;
            setMethodWrapperIl.Emit(OpCodes.Ret);
        }
    }
}
