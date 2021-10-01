using System;
using System.ComponentModel;

namespace AutoPropertyNotifier
{
    class Program
    {
        static void Main(string[] args)
        {
            User user = ProxyProvider.Instance.NewProxy<User>();

            ((INotifyPropertyChanged)user).PropertyChanged += User_PropertyChanged;

            user.Name = "hello";

            user.Name = "wow";

            user.Age = 4;

            Console.ReadLine();
        }

        private static void User_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Console.WriteLine(e.PropertyName + " is now : " + sender.GetType().GetProperty(e.PropertyName).GetValue(sender));
        }
    }
}
