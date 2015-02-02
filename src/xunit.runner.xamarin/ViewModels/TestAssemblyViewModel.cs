﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;
using Xunit.Runners.Utilities;

namespace Xunit.Runners.ViewModels
{
    public class TestAssemblyViewModel : ViewModelBase
    {
        private readonly INavigation navigation;
        private readonly ITestRunner runner;
        private string detailText;
        private Color displayColor;
        private string displayName;
        private TestState result;
        private bool isBusy;
        private string searchQuery;
        private TestState resultFilter;
        private readonly FilteredCollectionView<TestCaseViewModel, Tuple<string, TestState>> filteredTests;
        private readonly ObservableCollection<TestCaseViewModel> allTests; 
        private CancellationTokenSource filterCancellationTokenSource;

        internal TestAssemblyViewModel(INavigation navigation, IGrouping<string, TestCaseViewModel> @group, ITestRunner runner)
        {
            this.navigation = navigation;
            this.runner = runner;

            RunTestsCommand = new Command(RunTests);

            DisplayName = Path.GetFileNameWithoutExtension(@group.Key);

            allTests = new ObservableCollection<TestCaseViewModel>(@group);

            filteredTests = new FilteredCollectionView<TestCaseViewModel, Tuple<string, TestState>>(
                allTests,
                IsTestFilterMatch,
                Tuple.Create(SearchQuery, ResultFilter),
                new TestComparer()
                );

            filteredTests.ItemChanged += (sender, args) => UpdateCaption();
            filteredTests.CollectionChanged += (sender, args) => UpdateCaption();

            Result = TestState.NotRun;


            UpdateCaption();

        }
   
        private void UpdateCaption()
        {
            var count = allTests.Count;
            
            if (count == 0)
            {
                DetailText = "no test was found inside this assembly";
                DetailColor = Color.FromHex("#ff7f00");
            }
            else
            {
                var outcomes = allTests.GroupBy(r => r.Result);

                var results = outcomes.ToDictionary(k => k.Key, v => v.Count());

                int positive;
                results.TryGetValue(TestState.Passed, out positive);

                int failure;
                results.TryGetValue(TestState.Failed, out failure);

                int skipped;
                results.TryGetValue(TestState.Skipped, out skipped);

                int notRun;
                results.TryGetValue(TestState.NotRun, out notRun);

                // No failures and all run
                if (failure == 0 && notRun == 0)
                {
                    DetailText = string.Format("Success! {0} test{1}",
                                             positive, positive == 1 ? string.Empty : "s");
                    DetailColor = Color.Green;

                    Result = TestState.Passed;

                }
                else if (failure > 0 || (notRun > 0 && notRun < count))
                {
                    // we either have failures or some of the tests are not run
                    DetailText = string.Format("{0} success, {1} failure{2}, {3} skip{4}, {5} not run",
                                             positive, failure, failure > 1 ? "s" : String.Empty,
                                             skipped, skipped > 1 ? "s" : String.Empty,
                                             notRun);

                    DetailColor = Color.Red;

                    Result = TestState.Failed;
                }
                else if (Result == TestState.NotRun)
                {
                    DetailText = string.Format("{0} test case{1}, {2}",
                        count, count == 1 ? String.Empty : "s", Result);
                    DetailColor = Color.Green;
                }
            }
            
        }

        private static bool IsTestFilterMatch(TestCaseViewModel test, Tuple<string, TestState> query)
        {
            if (test == null) throw new ArgumentNullException("test");
            if (query == null) throw new ArgumentNullException("query");

            TestState? requiredTestState;
            switch (query.Item2)
            {
                case TestState.All:
                    requiredTestState = null;
                    break;
                case TestState.Passed:
                    requiredTestState = TestState.Passed;
                    break;
                case TestState.Failed:
                    requiredTestState = TestState.Failed;
                    break;
                case TestState.Skipped:
                    requiredTestState = TestState.Skipped;
                    break;
                case TestState.NotRun:
                    requiredTestState = TestState.NotRun;
                    break;
                default:
                    throw new ArgumentException();
            }

            if (requiredTestState.HasValue && test.Result != requiredTestState.Value)
            {
                return false;
            }

            var pattern = query.Item1;
            return string.IsNullOrWhiteSpace(pattern) || test.UniqueName.IndexOf(pattern.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public string SearchQuery
        {
            get { return searchQuery; }
            set
            {
                if (Set(ref searchQuery, value))
                {
                    this.FilterAfterDelay();
                }
            }
        }
        
        public TestState ResultFilter
        {
            get { return resultFilter; }
            set
            {
                if (Set(ref resultFilter, value))
                {
                    this.FilterAfterDelay();
                }
            }
        }

        public bool IsBusy
        {
            get { return isBusy; }
            private set { Set(ref isBusy, value); }
        }

        public TestState Result
        {
            get { return result; }
            set { Set(ref result, value); }
        }

        public string DisplayName
        {
            get { return displayName; }
            private set { Set(ref displayName, value); }
        }

        public Color DetailColor
        {
            get { return displayColor; }
            private set { Set(ref displayColor, value); }
        }

        public string DetailText
        {
            get { return detailText; }
            private set { Set(ref detailText, value); }
        }


        public IList<TestCaseViewModel> TestCases
        {
            get { return filteredTests; }
        }

        public ICommand RunTestsCommand { get; private set; }

        private async void RunTests()
        {
            try
            {
                IsBusy = true;
                await runner.Run(allTests);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void FilterAfterDelay()
        {
            if (this.filterCancellationTokenSource != null)
            {
                this.filterCancellationTokenSource.Cancel();
            }

            this.filterCancellationTokenSource = new CancellationTokenSource();
            var token = this.filterCancellationTokenSource.Token;

            Task
                .Delay(500, token)
                .ContinueWith(
                    x =>
                    {
                        filteredTests.FilterArgument = Tuple.Create(SearchQuery, ResultFilter);
                    },
                    token,
                    TaskContinuationOptions.None,
                    TaskScheduler.FromCurrentSynchronizationContext());
        }

        private class TestComparer : IComparer<TestCaseViewModel>
        {
            public int Compare(TestCaseViewModel x, TestCaseViewModel y)
            {
                int compare = string.Compare(x.DisplayName, y.DisplayName, StringComparison.OrdinalIgnoreCase);
                if (compare != 0)
                {
                    return compare;
                }

                return string.Compare(x.UniqueName, y.UniqueName, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}