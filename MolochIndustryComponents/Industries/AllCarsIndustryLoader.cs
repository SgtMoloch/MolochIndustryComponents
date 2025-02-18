using System.Collections.Generic;
using System.Linq;
using Model.Ops;
using Serilog;
using UnityEngine;

namespace MolochIndustryComponents;


public class AllCarsIndustryLoader : IndustryLoader
{
    private Serilog.ILogger logger = Log.ForContext(typeof(AllCarsIndustryLoader));

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

        ctx.AddToStorage(load, IndustryComponent.RateToValue(contractAdjustedProductionRate, ctx.DeltaTime), maxStorage);
        float quantityInStorage = ctx.QuantityInStorage(load);

        if (quantityInStorage < contractAdjustedCarLoadRate * cars.Count)
        {
            actualAmountToLoad /= cars.Count;
        }

        logger.Debug("Loader set to load at a rate of {0} (based on dT: {1} per day) . Since there are {2} cars able to be loaded, adjusting rate to {3} per car (based on dT and {4} quantity in storage)",
                    quantityToLoad, contractAdjustedCarLoadRate, cars.Count, actualAmountToLoad, quantityInStorage);

        foreach (IOpsCar car in cars)
        {
            float actualAmountLoaded = car.Load(load, quantityToLoad);

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