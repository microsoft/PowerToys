// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerLauncher.ViewModel;
using Wox.Plugin;

namespace Wox.Test
{
    [TestClass]
    public class WoxTest
    {
        // A Dummy class to test that OnPropertyChanged() is called while we set the variable
        private class DummyTestClass : BaseModel
        {
            public bool IsFunctionCalled { get; set; }

            private ICommand _item;

            public ICommand Item
            {
                get
                {
                    return _item;
                }

                set
                {
                    if (value != _item)
                    {
                        _item = value;
                        OnPropertyChanged();
                    }
                }
            }

            // Overriding the OnPropertyChanged() function to test if it is being called
            protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                IsFunctionCalled = true;
            }
        }

        [TestMethod]
        public void AnyVariableMustCallOnPropertyChangedWhenSet()
        {
            // Arrange
            DummyTestClass testClass = new DummyTestClass();

            // Act
            testClass.Item = new RelayCommand(null);

            // Assert
            Assert.IsTrue(testClass.IsFunctionCalled);
        }
    }
}
