using System;
using System.Collections.Generic;
using System.Text;

namespace IKoshelev.Mapper
{
    public interface IMapper<TSource, TDestination>
    {
        TDestination Map(TSource source);

        void Map(TSource source, TDestination destination);

        IMappingComponents<TSource, TDestination> MappingComponents
        {
            get;
        }
    }

    public class MapperBase<TSource, TDestination> : IMapper<TSource, TDestination>
    {
        public MapperBase(MappingComponentsBase<TSource, TDestination> mappingComponents)
        {
            MappingComponents = mappingComponents;
            compiledMappingFunction = mappingComponents.CombinedMappings.Compile();
        }

        internal Func<TSource, TDestination> compiledMappingFunction;

        public IMappingComponents<TSource, TDestination> MappingComponents
        {
            get; private set;
        }

        public TDestination Map(TSource source)
        {
            var result = compiledMappingFunction(source);
            return result;
        }

        public void Map(TSource source, TDestination destination)
        {
            throw new System.NotImplementedException();
        }
    }
}
