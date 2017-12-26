using System;
using System.Collections.Generic;
using System.Text;

namespace IKoshelev.Mapper
{
    public interface IMapper<TSource, TDestination> where TDestination : new()
    {
        TDestination Map(TSource source);

        void Map(TSource source, TDestination destination);
    }

    public class ExpressionMapper<TSource, TDestination> : IMapper<TSource, TDestination> where TDestination : new()
    {
        public ExpressionMapper(ExpressionMappingComponents<TSource, TDestination> mappingComponents)
        {
            MappingComponents = mappingComponents;
            compiledMappingFunctionWithConstructor = mappingComponents
                                                        .CombinedMappingsWithConstructor
                                                        .Compile();

            compiledMappingFunctionForExisting = mappingComponents
                                                        .CombinedMappingsForExistingTarget
                                                        .Compile();
        }

        internal Func<TSource, TDestination> compiledMappingFunctionWithConstructor;
        internal Action<TSource, TDestination> compiledMappingFunctionForExisting;

        public IMappingComponents<TSource, TDestination> MappingComponents
        {
            get; private set;
        }

        public TDestination Map(TSource source)
        {
            var result = compiledMappingFunctionWithConstructor(source);
            return result;
        }

        public void Map(TSource source, TDestination destination)
        {
            compiledMappingFunctionForExisting(source, destination);
        }
    }

    public class DelegateMapper<TSource, TDestination> : IMapper<TSource, TDestination> where TDestination : new()
    {
        public DelegateMapper(Action<TSource, TDestination> map)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
        }

        private Action<TSource, TDestination> map;

        public TDestination Map(TSource source)
        {
            TDestination destination = new TDestination();
            Map(source, destination);
            return destination;
        }

        public void Map(TSource source, TDestination destination)
        {
            map(source, destination);
        }
    }
}
