using System.Collections.Generic;
using System.Linq;
using Model.Ops;
using Serilog;
using UnityEngine;

namespace MolochIndustryComponents;


public class HopperUnloader : IndustryUnloader
{
	private bool _warnedConsumption;

	private string KeyUnloadedTotal => "unloaded-total-" + load.id;

	private Serilog.ILogger logger = Log.ForContext(typeof(HopperUnloader));

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

		foreach (IOpsCar car in cars)
		{
            float amountInCar = car.QuantityOfLoad(load).quantity;

			float amountToUnload = Mathf.Min(quantityToUnload * amountInCar, Mathf.Max(0f, contractAdjustedMaxStorage - quantityInStorage));

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