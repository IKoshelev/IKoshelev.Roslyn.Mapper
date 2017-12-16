using System;
using System.Linq.Expressions;
using IKoshelev.Mapper.MemberInitBindingsCombiner;

namespace IKoshelev.Mapper
{
    public abstract class MappingComponentsBase<TSource, TDestination> : IMappingComponents<TSource, TDestination>
    {
        public virtual Expression<Func<TSource, object>>[] SourceIgnoredProperties => new Expression<Func<TSource, object>>[0];
        public virtual Expression<Func<TDestination, object>>[] TargetIgnoredProperties => new Expression<Func<TDestination, object>>[0];

        public abstract Expression<Func<TSource, TDestination>> DefaultMappings
        {
            get;
        }

        public virtual Expression<Func<TSource, TDestination>> CustomMappings => null;

        public Expression<Func<TSource, TDestination>> CombinedMappings
        {
            get
            {
                var defaultMappings = DefaultMappings;
                var customMappings = CustomMappings;             

                if(customMappings == null)
                {
                    return customMappings;
                }

                var combined = new MemberInitBindingsCombiner<TSource, TDestination>()
                                                    .CombineIntoMapperWithConstructor(customMappings, defaultMappings);

                return combined;
            }
        }
    }
}