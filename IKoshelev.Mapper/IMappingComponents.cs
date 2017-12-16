using System;
using System.Linq.Expressions;

namespace IKoshelev.Mapper
{
    public interface IMappingComponents<TSource, TDestination>
    {
        Expression<Func<TSource, TDestination>> DefaultMappings
        {
            get;
        }
        Expression<Func<TSource, TDestination>> CustomMappings
        {
            get;
        }
        Expression<Func<TSource, object>>[] SourceIgnoredProperties { get; }
        Expression<Func<TDestination, object>>[] TargetIgnoredProperties { get; }
    }
}
