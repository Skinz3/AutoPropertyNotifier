using System;
using System.Collections.Generic;
using System.Text;

namespace AutoPropertyNotifier
{
    public class ProxyProvider
    {
        private static volatile ProxyProvider _instance = null;

        public static ProxyProvider Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ProxyProvider();
                }
                return _instance;
            }
        }

        private ProxyProvider()
        {
        }

        private Dictionary<Type, Type> _proxyTypes = new Dictionary<Type, Type>();

        public T NewProxy<T>()
        {
            return (T)ProxyHelper(typeof(T));
        }

        private object ProxyHelper(Type type)
        {
            Type proxyType;
            if (_proxyTypes.TryGetValue(type, out proxyType))
            {
                return Activator.CreateInstance(proxyType);
            }
            else
            {
                proxyType =
                INotifyPropertyChangedProxyTypeGenerator
                .GenerateProxy(type);
                _proxyTypes.Add(type, proxyType);
                return Activator.CreateInstance(proxyType);
            }
        }

        public object NewProxy(Type type)
        {
            return ProxyHelper(type);
        }
    }
}
