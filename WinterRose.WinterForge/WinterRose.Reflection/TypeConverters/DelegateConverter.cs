using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.Reflection.TypeConverters;
internal class DelegateConverter<TSource, TTarget> : ITypeConverter
{
    private readonly Func<TSource, TTarget> converter;

    /// <summary>
    /// The type of <typeparamref name="TSource"/>
    /// </summary>
    public Type SourceType => typeof(TSource);
    /// <summary>
    /// The type of <typeparamref name="TTarget"/>
    /// </summary>
    public Type TargetType => typeof(TTarget);

    public DelegateConverter(Func<TSource, TTarget> converter)
    {
        this.converter = converter;
    }

    public object Convert(object source) => converter((TSource)source);
}
