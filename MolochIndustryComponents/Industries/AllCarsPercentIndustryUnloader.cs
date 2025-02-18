using System.Collections.Generic;
using System.Linq;
using Model.Ops;
using Serilog;
using UnityEngine;

namespace MolochIndustryComponents;


public class AllCarsPercentIndustryUnloader : IndustryUnloader
{
	private bool _warnedConsumption;

	private string KeyUnloadedTotal => "unloaded-total-" + load.id;

	private Serilog.ILogger logger = Log.ForContext(typeof(AllCarsPercentIndustryUnloader));

	public override void Service(IIndustryContext ctx)
	{
		float contractMultiplier = base.Industry.GetContractMultiplier();
		float contractAdjustedMaxStorage = maxStorage * contractMultiplier;
		float contractAdjustedCarUnloadRate = carUnloadRate * contractMultiplier;
		float quantityInStorage = ctx.QuantityInStorage(load);
		float zeroThreshold = load.ZeroThreshold;
		float quantityToUnload = IndustryComponent.RateToValue(contractAdjustedCarUnloadRate, ctx.DeltaTime);
		float amountUnloaded = 0f;

		List<IOpsCar> cars = (from car in EnumerateCars(ctx, requireWaybill: true)
							  where car.IsEmptyOrContains(load)
							  orderby car.QuantityOfLoad(load).quantity
							  select car).ToList();
		float totalAmountInCars = cars.Sum(car => car.QuantityOfLoad(load).quantity);

		if (quantityToUnload < zeroThreshold)
		{
			if (!_warnedConsumption && contractMultiplier > 0f)
			{
				logger.Warning("Industry {0} {1} is less than {2}, this will be corrected each tick", base.Identifier, quantityToUnload, zeroThreshold);
				_warnedConsumption = true;
			}
			quantityToUnload = zeroThreshold * 2f;
		}

		float actualAmtToUnload = quantityToUnload / cars.Count;

		foreach (IOpsCar car in cars)
		{
			float amountToUnload = Mathf.Min(actualAmtToUnload, Mathf.Max(0f, contractAdjustedMaxStorage - quantityInStorage));

			if (amountToUnload < zeroThreshold)
			{
				break;
			}

			float percentToUnload = 1.0f;
			float carQuantity = car.QuantityOfLoad(load).quantity;

			float percentOfTotal = carQuantity / totalAmountInCars;

			percentToUnload = percentOfTotal;

			amountToUnload *= percentToUnload;

			logger.Debug("Unloader set to unload at a rate of {0} (based on dT: {1} per day). Since there are {2} cars able to be unloaded, adjusting rate to {3} for car {4} (based on dT and percentage)",
						quantityToUnload, contractAdjustedCarUnloadRate, cars.Count, amountToUnload, car.DisplayName);

			float unloadedAmt = car.Unload(load, amountToUnload);
			
			if (unloadedAmt < zeroThreshold)
			{
				if (orderAwayEmpties)
				{
					ctx.OrderAwayEmpty(car);
				}
				else if (car.Waybill.Value.Completed)
				{
					car.SetWaybill(null, this, "Empty completed");
				}
			}

			ctx.AddToStorage(load, unloadedAmt, contractAdjustedMaxStorage);

			quantityToUnload -= unloadedAmt;
			amountUnloaded += unloadedAmt;
		}

		if (load.payPerQuantity > 0f)
		{
			ctx.CounterIncrement(KeyUnloadedTotal, amountUnloaded);
		}

		ctx.RemoveFromStorage(load, IndustryComponent.RateToValue(contractMultiplier * storageConsumptionRate, ctx.DeltaTime));
	}
}