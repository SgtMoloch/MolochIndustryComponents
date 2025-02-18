using System.Collections.Generic;
using System.Linq;
using Model.Ops;
using Serilog;
using UnityEngine;

namespace MolochIndustryComponents;


public class AllCarsPercentIndustryLoader : IndustryLoader
{
	private Serilog.ILogger logger = Log.ForContext(typeof(AllCarsPercentIndustryLoader));

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

		float totalAmountInCars = cars.Sum(car => car.QuantityOfLoad(load).quantity);
		actualAmountToLoad = quantityToLoad / cars.Count;

		ctx.AddToStorage(load, IndustryComponent.RateToValue(contractAdjustedProductionRate, ctx.DeltaTime), maxStorage);
		float quantityInStorage = ctx.QuantityInStorage(load);

		foreach (IOpsCar car in cars)
		{
			float amountToLoad = Mathf.Min(actualAmountToLoad, quantityInStorage);

			float percentToLoad = 1.0f;
			float carQuantity = car.QuantityOfLoad(load).quantity;

			float percentOfTotal = carQuantity / totalAmountInCars;

			percentToLoad = percentOfTotal;

			amountToLoad *= percentToLoad;
			logger.Debug("Loader set to load at a rate of {0} (based on dT: {1} per day). Since there are {2} cars able to be loaded, adjusting rate to {3} for car {4} (based on dT and percentage)",
						actualAmountToLoad, contractAdjustedCarLoadRate, cars.Count, amountToLoad, car.DisplayName);

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
}