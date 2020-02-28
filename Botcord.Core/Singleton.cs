using System;

namespace Botcord.Core
{
    public class Singleton<T> where T : class
    {
        private static readonly T _instance = Activator.CreateInstance<T>();

        protected Singleton() { }

        public static T Instance
        {
            get
            {
                return _instance;
            }
        }

        public void Instantiate() { }
    }
}
