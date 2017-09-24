﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Irixi_Aligner_Common.Classes.Converters
{
    public class BooleanToVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool? v = (bool?)value;
            if (v.HasValue && v.Value == true)
                return Visibility.Visible;
            else
                return Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {

            bool? ret = null;
            if ((Visibility)value == Visibility.Visible)
                ret = false;
            else
                ret = true;

            return ret;
        }
    }
}
