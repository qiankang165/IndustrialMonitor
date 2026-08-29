using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace IndustrialMonitor.Models
{
    public class RegisterData : INotifyPropertyChanged
    {
        private int _address;
        private int _value;
        private bool _hasChanged;

        public int Address
        {
            get => _address;
            set { _address = value; OnPropertyChanged(); }
        }

        public int Value
        {
            get => _value;
            set
            {
                if (_value != value)  // 值真的变了
                {
                    _value = value;
                    HasChanged = true;  // 标记为变化
                }
                OnPropertyChanged();
            }
        }

        public bool HasChanged
        {
            get => _hasChanged;
            set { _hasChanged = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
