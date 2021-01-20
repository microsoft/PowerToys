namespace Mages.Core.Runtime.Converters
{
    using System;

    sealed class TypeConverter
    {
        private readonly Type _from;
        private readonly Type _to;
        private readonly Func<Object, Object> _converter;

        public TypeConverter(Type from, Type to, Func<Object, Object> converter)
        {
            _from = from;
            _to = to;
            _converter = converter;
        }

        public static TypeConverter Create<TFrom, TTo>(Func<TFrom, Object> converter)
        {
            return new TypeConverter(typeof(TFrom), typeof(TTo), x => converter((TFrom)x));
        }

        public Type From
        {
            get { return _from; }
        }

        public Type To
        {
            get { return _to; }
        }

        public Func<Object, Object> Converter
        {
            get { return _converter; }
        }
    }
}
