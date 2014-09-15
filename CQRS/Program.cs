using System;
using System.Collections.Generic;
using System.Linq;

namespace CQRS
{
    class Program
    {
        static void Main(string[] args)
        {


        }
    }

    public interface IMessagePublisher
    {
        List<object> Messages { get; }
        void Publish<T>(T message);
    }

    public class FakePublisher : IMessagePublisher
    {
        public List<object> Messages { get; set; }

        public FakePublisher(List<object> messages)
        {
            Messages = messages;
        }

        public void Publish<T>(T message)
        {
            Messages.Add(message);
        }
    }

    public class Manager
    {
        private readonly IMessagePublisher _publisher;

        public Manager(IMessagePublisher publisher)
        {
            _publisher = publisher;
        }

        public List<decimal> UpWindow = new List<decimal>();
        public List<decimal> DownWindow = new List<decimal>();
        public decimal SellThreshold { get; set; }
        public bool Runnable = true;

        public void HandlePositionAnquired(PositionAquired message)
        {
            if (!Runnable)
            {
                return;
            }
            SellThreshold = message.Price - 0.1m;
        }

        public void HandlePriceUpdated(PriceUpdated message)
        {
            if (!Runnable)
            {
                return;
            }
            UpWindow.Add(message.Price);
            DownWindow.Add(message.Price);

            _publisher.Publish(new FutureMessage<DelayedPriceUp>(TimeSpan.FromSeconds(10), new DelayedPriceUp(message.Price)));
            _publisher.Publish(new FutureMessage<DelayedPriceDown>(TimeSpan.FromSeconds(7), new DelayedPriceDown(message.Price)));
        }

        public void HandleDelayedPriceUp(DelayedPriceUp message)
        {
            if (!Runnable)
            {
                return;
            }

            UpWindow.Remove(message.Price);

            if (StableUpPrice - 0.1m > SellThreshold)
            {
                SellThreshold = StableUpPrice - 0.1m;
            }
        }

        public void HandleDelayedPriceDown(DelayedPriceDown message)
        {
            if (!Runnable)
            {
                return;
            }

            DownWindow.Remove(message.Price);

            if (StableDownPrice < SellThreshold)
            {
                _publisher.Publish(new Sell());
                Runnable = false;
            }
        }

        public decimal StableUpPrice
        {
            get { return UpWindow.Min(); }
        }

        public decimal StableDownPrice
        {
            get { return DownWindow.Max(); }
        }
    }



    public class PositionAquired : PriceUpdated
    {
        public PositionAquired(decimal target)
            : base(target) { }

    }

    public class FutureMessage<T>
    {
        public FutureMessage(TimeSpan delay, T message)
        {
            Delay = delay;
            Message = message;
        }

        public TimeSpan Delay { get; set; }
        public T Message { get; set; }
    }




    public class PriceUpdated
    {
        public PriceUpdated(decimal price)
        {
            Price = price;
        }

        public decimal Price { get; set; }
    }

    public class DelayedPriceUp
    {
        public DelayedPriceUp(decimal price)
        {
            Price = price;
        }

        public decimal Price { get; set; }
    }

    public class DelayedPriceDown
    {
        public DelayedPriceDown(decimal price)
        {
            Price = price;
        }

        public decimal Price { get; set; }
    }

    public class Sell
    {

    }


}
