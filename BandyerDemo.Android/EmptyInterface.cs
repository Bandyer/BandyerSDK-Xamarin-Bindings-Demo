using System;
using Com.Bandyer.Communication_center.Call;

namespace BandyerDemo.Droid
{
    public interface ICommCall : ICall
    {
    }

    public interface ICommEvent : IOnCallEventObserver { }
}
