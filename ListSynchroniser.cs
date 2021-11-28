using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CryptoOrderTrackerLambda
{
    public class ListSynchronizer<TSource, TDestination>
    {
        public Func<TSource, TDestination, bool> CompareFunction { get; set; }
        public Action<TDestination> RemoveAction { get; set; }
        public Action<TSource> AddAction { get; set; }
        public Action<TSource, TDestination> UpdateAction { get; set; }


        // USE THIS CONSTRUCTOR IF YOU INTEND TO ONLY USE COMPARE BY INDEX.
        public ListSynchronizer()
        {
        }

        public ListSynchronizer(Func<TSource, TDestination, bool> compareFunction)
        {
            CompareFunction = compareFunction ?? throw new ArgumentNullException(nameof(compareFunction));
        }

        public CompareResult<TSource, TDestination> Compare(ICollection<TSource> sourceList, ICollection<TDestination> destinationList)
        {
            var output = new CompareResult<TSource, TDestination>();

            // ITEMS IN LIST1 BUT NOT IN LIST 2.
            foreach (var list1Item in sourceList.Where(x => x != null))
            {
                var list2Item = destinationList.FirstOrDefault(x => CompareFunction(list1Item, x));
                if (list2Item != null)
                {
                    output.SourceDestinationMapAdd(list1Item, list2Item);
                }
                else
                {
                    output.NotInDestinationAdd(list1Item);
                }
            }

            // ITEMS IN LIST1 BUT NOT IN LIST 2.
            foreach (var list2Item in destinationList.Where(x => x != null))
            {
                if (sourceList.Any(x => CompareFunction(x, list2Item)) == false)
                {
                    output.NotInSourceAdd(list2Item);
                }
            }

            if (output.SourceDestinationMap.Count() + output.NotInDestination.Count() != sourceList.Count) throw new Exception("List 1 Count error");
            if (output.SourceDestinationMap.Count() + output.NotInSource.Count() != destinationList.Count)
            {
                throw new Exception("List 2 Count error")
                {
                    Data =
                    {
                        {"SourceDestinationMapCount", output.SourceDestinationMap?.Count()},
                        {"NotInSourceCount", output.NotInSource?.Count()},
                        {"destinationList", destinationList?.Count()}
                    }
                };
            }

            return output;
        }

        /// <summary>
        /// Compare the items by their index positions only.
        /// </summary>
        /// <param name="sourceList"></param>
        /// <param name="destinationList"></param>
        /// <returns></returns>
        public CompareResult<TSource, TDestination> CompareByIndex(IList<TSource> sourceList, IList<TDestination> destinationList)
        {
            var output = new CompareResult<TSource, TDestination>();

            // ITEMS IN LIST1 BUT NOT IN LIST 2.
            // NOT IN DESTINATION.
            if (sourceList.Count() > destinationList.Count)
            {
                for (var i = destinationList.Count(); i < sourceList.Count; i++)
                {
                    output.NotInDestinationAdd(sourceList[i]);
                }
            }
            else if (destinationList.Count() > sourceList.Count)
            {
                for (var i = sourceList.Count(); i < destinationList.Count; i++)
                {
                    output.NotInSourceAdd(destinationList[i]);
                }
            }

            // MAP 
            var mapCountMax = Math.Min(sourceList.Count, destinationList.Count);
            for (var i = 0; i < mapCountMax; i++)
            {
                output.SourceDestinationMapAdd(sourceList[i], destinationList[i]);
            }

            if (output.SourceDestinationMap.Count() + output.NotInDestination.Count() != sourceList.Count) throw new Exception("List 1 Count error");
            if (output.SourceDestinationMap.Count() + output.NotInSource.Count() != destinationList.Count) throw new Exception("List 2 Count error");

            return output;
        }


        public void Synchronize(ICollection<TSource> sourceItems, ICollection<TDestination> destinationItems)
        {
            if (CompareFunction == null) throw new NullReferenceException(nameof(CompareFunction));
            if (RemoveAction == null) throw new NullReferenceException(nameof(RemoveAction));
            if (AddAction == null) throw new NullReferenceException(nameof(AddAction));
            if (UpdateAction == null) throw new NullReferenceException(nameof(UpdateAction));

            // Remove items not in source from destination
            RemoveItems(sourceItems, destinationItems);

            // Add items in source to destination 
            AddOrUpdateItems(sourceItems, destinationItems);
        }

        private void RemoveItems(ICollection<TSource> sourceCollection, IEnumerable<TDestination> destinationCollection)
        {
            foreach (var destinationItem in destinationCollection.ToArray())
            {
                var sourceItem = sourceCollection.FirstOrDefault(item => CompareFunction(item, destinationItem));

                if (sourceItem == null)
                {
                    RemoveAction(destinationItem);
                }
            }
        }

        private void AddOrUpdateItems(IEnumerable<TSource> sourceCollection, IEnumerable<TDestination> destinationCollection)
        {
            var destinationList = destinationCollection.ToList();
            foreach (var sourceItem in sourceCollection)
            {
                var destinationItem = destinationList.FirstOrDefault(item => CompareFunction(sourceItem, item));

                if (destinationItem == null)
                {
                    AddAction(sourceItem);
                }
                else
                {
                    UpdateAction(sourceItem, destinationItem);
                }
            }
        }
    }


    public class CompareResult<TSource, TDestination>
    {
        #region Fields
        private readonly IList<TSource> _notInDestination;
        private readonly IList<TDestination> _notInSource;
        private readonly IList<(TSource list1Item, TDestination list2Item)> _sourceDestinationMap;
        #endregion

        #region Properties
        public IEnumerable<TSource> NotInDestination => _notInDestination;

        public IEnumerable<TDestination> NotInSource => _notInSource;

        public IEnumerable<(TSource SourceItem, TDestination DestinationItem)> SourceDestinationMap => _sourceDestinationMap;
        #endregion

        #region Constructors
        public CompareResult()
        {
            _notInSource = new List<TDestination>();
            _notInDestination = new List<TSource>();
            _sourceDestinationMap = new List<(TSource SourceItem, TDestination DestinationItem)>();
        }
        #endregion

        #region Methods
        internal void NotInDestinationAdd(TSource sourceItem)
        {
            _notInDestination.Add(sourceItem);
        }

        internal void NotInSourceAdd(TDestination destinationItem)
        {
            _notInSource.Add(destinationItem);
        }

        internal void SourceDestinationMapAdd(TSource sourceItem, TDestination destinationItem)
        {
            _sourceDestinationMap.Add((list1Item: sourceItem, list2Item: destinationItem));

        }
        #endregion
    }
}
