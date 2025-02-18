using System.Collections.Generic;
using System.Linq;
using Model;
using Model.Ops;
using Serilog;
using UnityEngine;

namespace MolochIndustryComponents;


public class MovingIndustryLoader : IndustryLoader
{
	private Serilog.ILogger logger = Log.ForContext(typeof(MovingIndustryLoader));

    private OpsController _controller = OpsController.Shared;

	public override void Service(IIndustryContext ctx)
	{
		float contractMultiplier = base.Industry.GetContractMultiplier();
		float contractAdjustedProductionRate = productionRate * contractMultiplier;
		float contractAdjustedCarLoadRate = carLoadRate * contractMultiplier;
		float quantityToLoad = IndustryComponent.RateToValue(contractAdjustedCarLoadRate, ctx.DeltaTime);
		float actualAmountToLoad = quantityToLoad;

		List<IOpsCar> cars = (from car in EnumerateCars(ctx, requireWaybill: true)
							  where car.IsEmptyOrContains(load)
							  orderby car.QuantityOfLoad(load).quantity descending
							  select car).ToList();

		actualAmountToLoad = quantityToLoad / cars.Count;

		ctx.AddToStorage(load, IndustryComponent.RateToValue(contractAdjustedProductionRate, ctx.DeltaTime), maxStorage);
		float quantityInStorage = ctx.QuantityInStorage(load);

		foreach (IOpsCar car in cars)
		{
			float amountToLoad = Mathf.Min(actualAmountToLoad, quantityInStorage);

			float actualAmountLoaded = car.Load(load, amountToLoad);

			if (car.IsFull(load))
			{
				if (orderAwayLoaded)
				{
					ctx.OrderAwayLoaded(car);
				}
				else
				{
					car.SetWaybill(null, this, "Full");
				}
			}

			ctx.RemoveFromStorage(load, actualAmountLoaded);
			
			quantityInStorage -= actualAmountLoaded;
		}
	}


    private IEnumerable<IOpsCar> CarsAtPosition()
	{
		foreach (Car item in _controller.CarsAtPosition(this))
		{
			float num = 0.75f;
			if (!(Mathf.Abs(item.velocity) > num))
			{
				OpsCarAdapter opsCarAdapter = new OpsCarAdapter(item, _controller);
				yield return opsCarAdapter;
			}
		}
	}

    protected new IEnumerable<IOpsCar> EnumerateCars(IIndustryContext ctx, bool requireWaybill = false)
	{
		foreach (IOpsCar item in CarsAtPosition())
		{
			if (!CarTypeMatches(item))
			{
				continue;
			}
			if (requireWaybill)
			{
				Waybill? waybill = item.Waybill;
				if (!waybill.HasValue || !waybill.Value.Destination.Equals(this))
				{
					continue;
				}
			}
			yield return item;
		}
	}

    private bool CarTypeMatches(IOpsCar car)
	{
		string carType = car.CarType;
		return carTypeFilter.Matches(carType);
	}
}