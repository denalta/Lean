﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.DataFeeds.Enumerators.Factories;
using QuantConnect.Securities;
using QuantConnect.Securities.Equity;

namespace QuantConnect.Tests.Engine.DataFeeds.Enumerators.Factories
{
    public class LiveCustomDataSubscriptionEnumeratorFactoryTests
    {
        [TestFixture]
        public class WhenCreatingEnumeratorForRestData
        {
            private readonly DateTime _referenceLocal = new DateTime(2017, 10, 12);
            private readonly DateTime _referenceUtc = new DateTime(2017, 10, 12).ConvertToUtc(TimeZones.NewYork);

            private ManualTimeProvider _timeProvider;
            private IEnumerator<BaseData> _enumerator;
            private Mock<ISubscriptionDataSourceReader> dataSourceReader;

            [SetUp]
            public void Given()
            {
                _timeProvider = new ManualTimeProvider(_referenceUtc);

                dataSourceReader = new Mock<ISubscriptionDataSourceReader>();
                dataSourceReader.Setup(dsr => dsr.Read(It.Is<SubscriptionDataSource>(sds =>
                        sds.Source == "rest.source" &&
                        sds.TransportMedium == SubscriptionTransportMedium.Rest &&
                        sds.Format == FileFormat.Csv))
                    )
                    .Returns(Enumerable.Range(0, 100)
                        .Select(i => new RestData
                        {
                            EndTime = _referenceLocal.AddSeconds(i)
                        }))
                        .Verifiable();

                var quoteCurrency = new Cash(CashBook.AccountCurrency, 0, 1);
                var exchangeHours = MarketHoursDatabase.FromDataFolder().GetExchangeHours(Market.USA, Symbols.SPY, SecurityType.Equity);
                var config = new SubscriptionDataConfig(typeof(RestData), Symbols.SPY, Resolution.Second, TimeZones.NewYork, TimeZones.NewYork, false, false, false);
                var security = new Equity(Symbols.SPY, exchangeHours, quoteCurrency, SymbolProperties.GetDefault(CashBook.AccountCurrency));
                var request = new SubscriptionRequest(false, null, security, config, _referenceUtc, _referenceUtc.AddDays(1));

                var factory = new TestableLiveCustomDataSubscriptionEnumeratorFactory(_timeProvider, dataSourceReader.Object);
                _enumerator = factory.CreateEnumerator(request, null);
            }

            [Test]
            public void YieldsDataEachSecondAsTimePasses()
            {
                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNotNull(_enumerator.Current);
                Assert.AreEqual(_referenceLocal, _enumerator.Current.EndTime);

                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNull(_enumerator.Current);

                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNull(_enumerator.Current);

                _timeProvider.AdvanceSeconds(1);

                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNotNull(_enumerator.Current);
                Assert.AreEqual(_referenceLocal.AddSeconds(1), _enumerator.Current.EndTime);

                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNull(_enumerator.Current);

                dataSourceReader.Verify(dsr => dsr.Read(It.Is<SubscriptionDataSource>(sds =>
                        sds.Source == "rest.source" &&
                        sds.TransportMedium == SubscriptionTransportMedium.Rest &&
                        sds.Format == FileFormat.Csv))
                    , Times.Once);
            }
        }

        [TestFixture]
        public class WhenCreatingEnumeratorForRestCollectionData
        {
            private const int DataPerTimeStep = 3;
            private readonly DateTime _referenceLocal = new DateTime(2017, 10, 12);
            private readonly DateTime _referenceUtc = new DateTime(2017, 10, 12).ConvertToUtc(TimeZones.NewYork);

            private ManualTimeProvider _timeProvider;
            private IEnumerator<BaseData> _enumerator;
            private Mock<ISubscriptionDataSourceReader> dataSourceReader;

            [SetUp]
            public void Given()
            {
                _timeProvider = new ManualTimeProvider(_referenceUtc);

                dataSourceReader = new Mock<ISubscriptionDataSourceReader>();
                dataSourceReader.Setup(dsr => dsr.Read(It.Is<SubscriptionDataSource>(sds =>
                        sds.Source == "rest.collection.source" &&
                        sds.TransportMedium == SubscriptionTransportMedium.Rest &&
                        sds.Format == FileFormat.Collection))
                    )
                    .Returns(Enumerable.Range(0, 100)
                        .Select(i => new BaseDataCollection(_referenceLocal.AddSeconds(i), Symbols.SPY, Enumerable.Range(0, DataPerTimeStep)
                            .Select(_ => new RestCollectionData {EndTime = _referenceLocal.AddSeconds(i)})))
                    )
                    .Verifiable();

                var quoteCurrency = new Cash(CashBook.AccountCurrency, 0, 1);
                var exchangeHours = MarketHoursDatabase.FromDataFolder().GetExchangeHours(Market.USA, Symbols.SPY, SecurityType.Equity);
                var config = new SubscriptionDataConfig(typeof(RestCollectionData), Symbols.SPY, Resolution.Second, TimeZones.NewYork, TimeZones.NewYork, false, false, false);
                var security = new Equity(Symbols.SPY, exchangeHours, quoteCurrency, SymbolProperties.GetDefault(CashBook.AccountCurrency));
                var request = new SubscriptionRequest(false, null, security, config, _referenceUtc, _referenceUtc.AddDays(1));

                var factory = new TestableLiveCustomDataSubscriptionEnumeratorFactory(_timeProvider, dataSourceReader.Object);
                _enumerator = factory.CreateEnumerator(request, null);
            }

            [Test]
            public void YieldsGroupOfDataEachSecond()
            {
                for (int i = 0; i < DataPerTimeStep; i++)
                {
                    Assert.IsTrue(_enumerator.MoveNext());
                    Assert.IsNotNull(_enumerator.Current, $"Index {i} is null.");
                    Assert.AreEqual(_referenceLocal, _enumerator.Current.EndTime);
                }

                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNull(_enumerator.Current);

                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNull(_enumerator.Current);

                _timeProvider.AdvanceSeconds(1);

                for (int i = 0; i < DataPerTimeStep; i++)
                {
                    Assert.IsTrue(_enumerator.MoveNext());
                    Assert.IsNotNull(_enumerator.Current);
                    Assert.AreEqual(_referenceLocal.AddSeconds(1), _enumerator.Current.EndTime);
                }

                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNull(_enumerator.Current);

                dataSourceReader.Verify(dsr => dsr.Read(It.Is<SubscriptionDataSource>(sds =>
                        sds.Source == "rest.collection.source" &&
                        sds.TransportMedium == SubscriptionTransportMedium.Rest &&
                        sds.Format == FileFormat.Collection))
                    , Times.Once);
            }
        }

        [TestFixture]
        public class WhenCreatingEnumeratorForSecondRemoteFileData
        {
            private readonly DateTime _referenceLocal = new DateTime(2017, 10, 12);
            private readonly DateTime _referenceUtc = new DateTime(2017, 10, 12).ConvertToUtc(TimeZones.NewYork);

            private ManualTimeProvider _timeProvider;
            private IEnumerator<BaseData> _enumerator;
            private Mock<ISubscriptionDataSourceReader> dataSourceReader;

            [SetUp]
            public void Given()
            {
                _timeProvider = new ManualTimeProvider(_referenceUtc);

                dataSourceReader = new Mock<ISubscriptionDataSourceReader>();
                dataSourceReader.Setup(dsr => dsr.Read(It.Is<SubscriptionDataSource>(sds =>
                        sds.Source == "remote.file.source" &&
                        sds.TransportMedium == SubscriptionTransportMedium.RemoteFile &&
                        sds.Format == FileFormat.Csv))
                    )
                    .Returns(Enumerable.Range(0, 100)
                        .Select(i => new RemoteFileData
                        {
                            // include past data
                            EndTime = _referenceLocal.AddSeconds(i - 95)
                        }))
                    .Verifiable();

                var quoteCurrency = new Cash(CashBook.AccountCurrency, 0, 1);
                var exchangeHours = MarketHoursDatabase.FromDataFolder().GetExchangeHours(Market.USA, Symbols.SPY, SecurityType.Equity);
                var config = new SubscriptionDataConfig(typeof(RemoteFileData), Symbols.SPY, Resolution.Second, TimeZones.NewYork, TimeZones.NewYork, false, false, false);
                var security = new Equity(Symbols.SPY, exchangeHours, quoteCurrency, SymbolProperties.GetDefault(CashBook.AccountCurrency));
                var request = new SubscriptionRequest(false, null, security, config, _referenceUtc, _referenceUtc.AddDays(1));

                var factory = new TestableLiveCustomDataSubscriptionEnumeratorFactory(_timeProvider, dataSourceReader.Object);
                _enumerator = factory.CreateEnumerator(request, null);
            }

            [Test]
            public void YieldsDataEachSecondAsTimePasses()
            {
                // most recent 5 seconds of data
                for (int i = 5; i >= 0; i--)
                {
                    Assert.IsTrue(_enumerator.MoveNext());
                    Assert.IsNotNull(_enumerator.Current);
                    Assert.AreEqual(_referenceLocal.AddSeconds(-i), _enumerator.Current.EndTime);
                }

                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNull(_enumerator.Current);

                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNull(_enumerator.Current);

                _timeProvider.AdvanceSeconds(1);

                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNotNull(_enumerator.Current);
                Assert.AreEqual(_referenceLocal.AddSeconds(1), _enumerator.Current.EndTime);

                // these are rate limited by the refresh enumerator's func guard clause
                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNull(_enumerator.Current);

                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNull(_enumerator.Current);

                dataSourceReader.Verify(dsr => dsr.Read(It.Is<SubscriptionDataSource>(sds =>
                        sds.Source == "remote.file.source" &&
                        sds.TransportMedium == SubscriptionTransportMedium.RemoteFile &&
                        sds.Format == FileFormat.Csv))
                    , Times.Once);
            }
        }

        [TestFixture]
        public class WhenCreatingEnumeratorForDailyRemoteFileData
        {
            private int _dataPointsAfterReference = 1;
            private readonly DateTime _referenceLocal = new DateTime(2017, 10, 12);
            private readonly DateTime _referenceUtc = new DateTime(2017, 10, 12).ConvertToUtc(TimeZones.NewYork);

            private ManualTimeProvider _timeProvider;
            private IEnumerator<BaseData> _enumerator;
            private Mock<ISubscriptionDataSourceReader> dataSourceReader;

            [SetUp]
            public void Given()
            {
                _timeProvider = new ManualTimeProvider(_referenceUtc);

                dataSourceReader = new Mock<ISubscriptionDataSourceReader>();
                dataSourceReader.Setup(dsr => dsr.Read(It.Is<SubscriptionDataSource>(sds =>
                        sds.Source == "remote.file.source" &&
                        sds.TransportMedium == SubscriptionTransportMedium.RemoteFile &&
                        sds.Format == FileFormat.Csv))
                    )
                    .Returns(() => Enumerable.Range(0, 100)
                        .Select(i => new RemoteFileData
                        {
                            // include past data
                            EndTime = _referenceLocal.Add(TimeSpan.FromDays(i - (100 - _dataPointsAfterReference - 1)))
                        }))
                    .Verifiable();

                var quoteCurrency = new Cash(CashBook.AccountCurrency, 0, 1);
                var exchangeHours = MarketHoursDatabase.FromDataFolder().GetExchangeHours(Market.USA, Symbols.SPY, SecurityType.Equity);
                var config = new SubscriptionDataConfig(typeof(RemoteFileData), Symbols.SPY, Resolution.Daily, TimeZones.NewYork, TimeZones.NewYork, false, false, false);
                var security = new Equity(Symbols.SPY, exchangeHours, quoteCurrency, SymbolProperties.GetDefault(CashBook.AccountCurrency));
                var request = new SubscriptionRequest(false, null, security, config, _referenceUtc, _referenceUtc.AddDays(1));

                var factory = new TestableLiveCustomDataSubscriptionEnumeratorFactory(_timeProvider, dataSourceReader.Object);
                _enumerator = factory.CreateEnumerator(request, null);
            }

            [Test]
            public void YieldsDataEachDayAsTimePasses()
            {
                // previous point is exactly one resolution step behind, so it emits
                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNotNull(_enumerator.Current);
                Assert.AreEqual(_referenceLocal.AddDays(-1), _enumerator.Current.EndTime);

                // yields the current time
                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNotNull(_enumerator.Current);
                Assert.AreEqual(_referenceLocal, _enumerator.Current.EndTime);

                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNull(_enumerator.Current);

                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNull(_enumerator.Current);

                _timeProvider.Advance(Time.OneDay);

                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNotNull(_enumerator.Current);
                Assert.AreEqual(_referenceLocal.AddDays(1), _enumerator.Current.EndTime);

                // these are rate limited by the refresh enumerator's func guard clause
                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNull(_enumerator.Current);

                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNull(_enumerator.Current);

                _timeProvider.Advance(TimeSpan.FromMinutes(30));

                // now that time has moved forward 30 minutes we'll retry to get the next data point
                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNull(_enumerator.Current);

                _timeProvider.Advance(Time.OneDay);

                // now to the next day, we'll try again and get data
                _dataPointsAfterReference++;
                Assert.IsTrue(_enumerator.MoveNext());
                Assert.IsNotNull(_enumerator.Current);
                Assert.AreEqual(_referenceLocal.AddDays(2), _enumerator.Current.EndTime);

                dataSourceReader.Verify(dsr => dsr.Read(It.Is<SubscriptionDataSource>(sds =>
                        sds.Source == "remote.file.source" &&
                        sds.TransportMedium == SubscriptionTransportMedium.RemoteFile &&
                        sds.Format == FileFormat.Csv))
                    , Times.Exactly(4));
            }
        }

        class RestData : BaseData
        {
            public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
            {
                return new SubscriptionDataSource("rest.source", SubscriptionTransportMedium.Rest);
            }
        }

        class RestCollectionData : BaseData
        {
            public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
            {
                return new SubscriptionDataSource("rest.collection.source", SubscriptionTransportMedium.Rest, FileFormat.Collection);
            }
        }

        class RemoteFileData : BaseData
        {
            public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
            {
                return new SubscriptionDataSource("remote.file.source", SubscriptionTransportMedium.RemoteFile);
            }
        }

        class TestableLiveCustomDataSubscriptionEnumeratorFactory : LiveCustomDataSubscriptionEnumeratorFactory
        {
            private readonly ISubscriptionDataSourceReader _dataSourceReader;

            public TestableLiveCustomDataSubscriptionEnumeratorFactory(ITimeProvider timeProvider, ISubscriptionDataSourceReader dataSourceReader)
                : base(timeProvider)
            {
                _dataSourceReader = dataSourceReader;
            }

            protected override ISubscriptionDataSourceReader GetSubscriptionDataSourceReader(SubscriptionDataSource source,
                IDataCacheProvider dataCacheProvider,
                SubscriptionDataConfig config,
                DateTime date)
            {
                return _dataSourceReader;
            }
        }
    }
}
