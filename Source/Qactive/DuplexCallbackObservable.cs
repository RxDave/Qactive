﻿using System;
using System.Diagnostics.Contracts;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Runtime.ExceptionServices;

namespace Qactive
{
  [Serializable]
  internal sealed class DuplexCallbackObservable<T> : DuplexCallback, IObservable<T>
  {
    private readonly IScheduler scheduler;

    public DuplexCallbackObservable(int id, IScheduler scheduler)
      : base(id)
    {
      Contract.Requires(scheduler != null);

      this.scheduler = scheduler;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "There is no meaningful way to handle exceptions here other than passing them to a handler, and we cannot let them leave their contexts because they will be missed.")]
    public IDisposable Subscribe(IObserver<T> observer)
    {
      var disposables = new CompositeDisposable();

      Action<Action> tryExecute =
        action =>
        {
          disposables.Add(
            scheduler.Schedule(() =>
            {
              try
              {
                action();
              }
              catch (Exception ex)
              {
                Protocol.CancelAllCommunication(ExceptionDispatchInfo.Capture(ex));
              }
            }));
        };

      try
      {
        disposables.Add(
          Sink.Subscribe(
            Id,
            value => tryExecute(() => observer.OnNext((T)value)),
            ex => tryExecute(() => observer.OnError(ex)),
            () => tryExecute(observer.OnCompleted)));

        return disposables;
      }
      catch (Exception ex)
      {
        Protocol.CancelAllCommunication(ExceptionDispatchInfo.Capture(ex));

        disposables.Dispose();

        return Disposable.Empty;
      }
    }
  }
}