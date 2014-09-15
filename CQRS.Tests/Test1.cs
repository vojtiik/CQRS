using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace CQRS.Tests
{
    [TestFixture]
    public class Test1
    {
        private static IMessagePublisher _publisher;
        private Manager _manager;

        [SetUp]
        public void Setup()
        {
            _publisher = new FakePublisher(new List<object>());
            _manager = new Manager(_publisher);
        }

        [TestCase]
        public void WhenPriceUpdatedWeShouldSendTwoMessages()
        {
            _manager.HandlePriceUpdated(new PriceUpdated (123.0m));

            Assert.AreEqual(_publisher.Messages.Count, 2);
            _publisher.Messages.Count(x => x is FutureMessage<DelayedPriceUp> && ((FutureMessage<DelayedPriceUp>)x).Message.Price == 123.0m).Should().Be(1);
            _publisher.Messages.Count(x => x is FutureMessage<DelayedPriceDown> && ((FutureMessage<DelayedPriceDown>)x).Message.Price == 123.0m).Should().Be(1);
        }

        [TestCase]
        public void WhenPriceUpdatedPriceShouldBeAddedToWindows()
        {
            _manager.HandlePriceUpdated(new PriceUpdated(123.0m));

            _manager.UpWindow.Count.Should().Be(1);
            _manager.UpWindow.Should().Contain(123.0m);

            _manager.DownWindow.Count.Should().Be(1);
            _manager.DownWindow.Should().Contain(123.0m);
        }

        [TestCase]
        public void WhenPriceUpdatedWithTwoSimilarValuesPricesShouldBothBeAddedToWindows()
        {
            _manager.HandlePriceUpdated(new PriceUpdated (123.0m));
            _manager.HandlePriceUpdated(new PriceUpdated (123.0m));

            _manager.UpWindow.Count.Should().Be(2);
            _manager.UpWindow.Count(x => x == 123.0m).Should().Be(2);

            _manager.DownWindow.Count.Should().Be(2);
            _manager.DownWindow.Count(x => x == 123.0m).Should().Be(2);
        }

        [TestCase]
        public void WhenDelayedPriceUpMessageIsReceived_DoNotupdateDownWindow()
        {
            _manager.HandlePriceUpdated(new PriceUpdated(123.0m));
            _manager.HandlePriceUpdated(new PriceUpdated(123.0m));

            _manager.HandleDelayedPriceUp(new DelayedPriceUp(123.0m));
            _manager.DownWindow.Count.Should().Be(2);
        }

        [TestCase]
        public void WhenDelayedPriceDownMessageIsReceived_DoNotupdateUpWindow()
        {
            _manager.HandlePriceUpdated(new PriceUpdated(123.0m));
            _manager.HandlePriceUpdated(new PriceUpdated(123.0m));

            _manager.HandleDelayedPriceDown(new DelayedPriceDown(123.0m));
            _manager.UpWindow.Count.Should().Be(2);
        }

        [TestCase]
        public void WhenDelayedPriceUpMessageIsReceived_UpwindowIsUpdarted()
        {
            _manager.HandlePriceUpdated(new PriceUpdated(1234.0m));
            _manager.HandlePriceUpdated(new PriceUpdated(123.0m));
            _manager.HandlePriceUpdated(new PriceUpdated(123.0m));
            _manager.HandlePriceUpdated(new PriceUpdated(5678.0m));

            _manager.HandleDelayedPriceUp(new DelayedPriceUp(123.0m));
            _manager.UpWindow.Count.Should().Be(3);

            _manager.UpWindow.Count(x=> x == 123m).Should().Be(1);
        }


        [TestCase]
        public void WhenDelayedPriceDownMessageIsReceived_DownWindowIsUpdated()
        {
            _manager.HandlePriceUpdated(new PriceUpdated(1234.0m));
            _manager.HandlePriceUpdated(new PriceUpdated(123.0m));
            _manager.HandlePriceUpdated(new PriceUpdated(123.0m));
            _manager.HandlePriceUpdated(new PriceUpdated(5678.0m));

            _manager.HandleDelayedPriceDown(new DelayedPriceDown(123.0m));
            _manager.DownWindow.Count.Should().Be(3);

            _manager.DownWindow.Count(x => x == 123m).Should().Be(1);
        }


        [TestCase]
        public void WhenPriceUpwardsMessageArrives_TargetIsIncreasedByminimumMinusThePermittedLoss()
        {
            _manager.HandlePositionAnquired(new PositionAquired(1m));

            _manager.HandlePriceUpdated(new PriceUpdated(2));
            _manager.HandlePriceUpdated(new PriceUpdated(3));
            _manager.HandlePriceUpdated(new PriceUpdated(4));
            _manager.HandlePriceUpdated(new PriceUpdated(5));

            _manager.HandleDelayedPriceUp(new DelayedPriceUp(1));

            _manager.SellThreshold.Should().Be(1.9m);
        }

        [TestCase]
        public void WhenPriceDownwardsMessageArrives_TargetIsunchanged()
        {
            _manager.HandlePositionAnquired(new PositionAquired(10m));
            _manager.HandlePriceUpdated(new PriceUpdated(2));
            _manager.HandlePriceUpdated(new PriceUpdated(3));
            _manager.HandlePriceUpdated(new PriceUpdated(4));
            _manager.HandlePriceUpdated(new PriceUpdated(5));

            _manager.HandleDelayedPriceDown(new DelayedPriceDown(2));

            _manager.SellThreshold.Should().Be(9.9m);
        }



        [TestCase]
        public void WhenPriceDownwardsIsLessThan10CentsUnderTheTarget_Sell()
        {
            _manager.HandlePositionAnquired(new PositionAquired(10m));

            _manager.HandlePriceUpdated(new PriceUpdated(14));
            _manager.HandlePriceUpdated(new PriceUpdated(5));
            _manager.HandlePriceUpdated(new PriceUpdated(5));


            _manager.HandleDelayedPriceDown(new DelayedPriceDown(14));

            _publisher.Messages.Should().NotBeEmpty();
            _publisher.Messages.Last().Should().BeOfType<Sell>();
        }

        [TestCase]
        public void WhenPriceDownwardsIsMoreThan10CentsUnderTheTarget_DoNotSell()
        {
            _manager.HandlePositionAnquired(new PositionAquired(10m));

            _manager.HandlePriceUpdated(new PriceUpdated(14));
            _manager.HandlePriceUpdated(new PriceUpdated(5));
            _manager.HandlePriceUpdated(new PriceUpdated(5));


            _manager.HandleDelayedPriceDown(new DelayedPriceDown(5));

            
            var lastMessage = _publisher.Messages.Last();
            (lastMessage is Sell).Should().BeFalse();
        }


    }
}
