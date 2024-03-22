namespace MealPlanner

open System
open System.Collections.Generic

module Types =
    type AuditInfo =
        {
            CreatedBy: string
            CreatedDate: DateTime
            LastupdatedBy: string
            LastUpdatedDate: DateTime
        }

    type Meal =
        {
            Name : string
            RecipeId : Guid option
        }

    type MealPlanDay =
        {
            Breakfast : Meal
            Lunch : Meal
            Supper : Meal
            Other: Meal
        }

    type Meals =
        {
            Sunday : MealPlanDay
            Monday : MealPlanDay
            Tuesday : MealPlanDay
            Wednesday : MealPlanDay
            Thursday : MealPlanDay
            Friday : MealPlanDay
            Saturday : MealPlanDay
        }

    type MealPlanWeek =
        {
            Id: Guid
            Meals : Meals
            WeekNumber: int
            Audit: AuditInfo
        }

    type MealPlan =
        {
            Id: Guid
            Weeks: IEnumerable<MealPlanWeek>
        }

    type PingInfo =
        {
            Name : string
            Version : string
            Database : string
        }

module Config =
    [<CLIMutable>]
    type Metadata =
        {
            Version : string
            Name : string

        }
