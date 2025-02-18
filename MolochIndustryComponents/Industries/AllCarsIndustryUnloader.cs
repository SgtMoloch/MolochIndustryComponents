using System.Collections.Generic;
using System.Linq;
using Model.Ops;
using Serilog;
using UnityEngine;

namespace MolochIndustryComponents;


public class AllCarsIndustryUnloader : IndustryUnloader
{
	private bool _warnedConsumption;

	private string KeyUnloadedTotal => "unloaded-total-" + load.id;

	private Serilog.ILogger logger = Log.ForContext(typeof(AllCarsIndustryUnloader));

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

		logger.Debug("Unloader set to unload at a rate of {0} (based on dT: {1} per day). Since there are {2} cars able to be unloaded, adjusting rate to {3} per car (based on dT)", quantityToUnload, contractAdjustedCarUnloadRate, cars.Count, actualAmtToUnload);

		foreach (IOpsCar car in cars)
		{
			float amountToUnload = Mathf.Min(actualAmtToUnload, Mathf.Max(0f, contractAdjustedMaxStorage - quantityInStorage));

			if (amountToUnload < zeroThreshold)
			{
				break;
			}

			float unloadedAmount = car.Unload(load, amountToUnload);

			if (unloadedAmount < zeroThreshold)
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

			ctx.AddToStorage(load, unloadedAmount, contractAdjustedMaxStorage);

			quantityToUnload -= unloadedAmount;
			amountUnloaded += unloadedAmount;
		}

		if (load.payPerQuantity > 0f)
		{
			ctx.CounterIncrement(KeyUnloadedTotal, amountUnloaded);
		}

		ctx.RemoveFromStorage(load, IndustryComponent.RateToValue(contractMultiplier * storageConsumptionRate, ctx.DeltaTime));
	}
}