namespace Mages.Core.Tests
{
    using NUnit.Framework;
    using Runtime;
    using System;
    using System.Collections.Generic;

    [TestFixture]
    public class ObservableTests
    {
        [Test]
        public void WrapOrdinaryDictionaryIsObservable()
        {
            var dict = new Dictionary<String, Object>();
            var obs = new ObservableDictionary(dict);
            var count = 0;
            obs.Changed += (s, ev) => count++;

            obs.Add("test", "string");
            Assert.AreEqual(1, count);
            Assert.AreEqual(1, dict.Count);
            Assert.AreEqual("string", dict["test"]);
        }

        [Test]
        public void ProvideObservableDictionaryAsScope()
        {
            var obs = new ObservableDictionary();
            var engine = new Engine(new Configuration { Scope = obs });
            var keys = new List<String>();
            obs.Changed += (s, ev) => keys.Add(ev.Key);
            engine.Interpret("x = 42");

            Assert.AreEqual(1, keys.Count);
            Assert.AreEqual("x", keys[0]);
            Assert.AreEqual(42.0, obs["x"]);
        }

        [Test]
        public void ChangeWrapperObjectToBeObservable()
        {
            var engine = new Engine(new Configuration { IsEngineExposed = true });
            var engineObj = engine.Globals["engine"] as IDictionary<String, Object>;
            var obs = new ObservableDictionary(engineObj);
            var keys = new List<String>();
            var version = obs["version"];
            engine.Globals["engine"] = obs;
            obs.Changed += (s, ev) => keys.Add(ev.Key);
            engine.Interpret("engine.version = \"my version\"");

            Assert.AreEqual(1, keys.Count);
            Assert.AreEqual("version", keys[0]);
            Assert.AreEqual(version, obs["version"]);
        }

        [Test]
        public void ClearObservableDictionaryEmitsEvents()
        {
            var obs = new ObservableDictionary(new Dictionary<String, Object>
            {
                { "x", 1.0 },
                { "y", 2.0 },
                { "z", 42.0 },
            });
            var keys = new List<String>();
            obs.Changed += (s, ev) => 
            {
                if (ev.NewValue == null && ev.OldValue != null)
                {
                    keys.Add(ev.Key);
                }
            };

            obs.Clear();
            Assert.AreEqual(3, keys.Count);
            Assert.AreEqual(0, obs.Count);
            CollectionAssert.AreEquivalent(new[] { "x", "y", "z" }, keys);
        }

        [Test]
        public void AddToObservableDictionaryIsTriggered()
        {
            var obs = new ObservableDictionary();
            var keys = new List<String>();
            obs.Changed += (s, ev) => keys.Add(ev.Key);

            obs.Add("test", 42.0);
            Assert.AreEqual(1, keys.Count);
            Assert.AreEqual(1, obs.Count);
            Assert.AreEqual(42.0, obs["test"]);
        }

        [Test]
        public void RemoveFromObservableDictionaryIsTriggered()
        {
            var obs = new ObservableDictionary();
            var keys = new List<String>();
            obs.Add("test", 42.0);
            obs.Changed += (s, ev) => keys.Add(ev.Key);
            obs.Remove("test");

            Assert.AreEqual(1, keys.Count);
            Assert.AreEqual(0, obs.Count);
        }

        [Test]
        public void RemoveNotFoundFromObservableDictionaryIsNotTriggered()
        {
            var obs = new ObservableDictionary();
            var keys = new List<String>();
            obs.Changed += (s, ev) => keys.Add(ev.Key);
            obs.Remove("test");

            Assert.AreEqual(0, keys.Count);
            Assert.AreEqual(0, obs.Count);
        }

        [Test]
        public void SetValueInObservableDictionaryIsTriggered()
        {
            var obs = new ObservableDictionary();
            var keys = new List<String>();
            obs.Changed += (s, ev) => keys.Add(ev.Key);
            obs["test"] = 17.0;

            Assert.AreEqual(1, keys.Count);
            Assert.AreEqual(1, obs.Count);
            Assert.AreEqual(17.0, obs["test"]);
        }

        [Test]
        public void ChangeValueInObservableDictionaryIsTriggered()
        {
            var obs = new ObservableDictionary();
            var keys = new List<String>();
            obs["test"] = false;
            obs.Changed += (s, ev) => keys.Add(ev.Key);
            obs["test"] = true;

            Assert.AreEqual(1, keys.Count);
            Assert.AreEqual(1, obs.Count);
            Assert.AreEqual(true, obs["test"]);
        }
    }
}
