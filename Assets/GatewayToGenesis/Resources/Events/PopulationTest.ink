=== population_test ===
# population_test
* [Check current population] -> population_check
* [Modify population] -> population_modify
* [Check housing] -> housing_check
* [Modify housing] -> housing_modify
* [Check vagrants] -> vagrants_check
* [Modify vagrants] -> vagrants_modify
* [Check deaths] -> deaths_check
* [Process deaths] -> deaths_process
* [Back to main] -> DONE

=== population_check ===
Current population: {GetPopulation()}
Current housing: {GetHousing()}
Current vagrants: {GetVagrants()}
Current deaths: {GetDeaths()}

* [Back] -> population_test

=== population_modify ===
* [Add 5 population] -> population_add
* [Remove 3 population] -> population_remove
* [Back] -> population_test

=== population_add ===
~ ModifyPopulation(5)
Population increased by 5!
Current population: {GetPopulation()}

* [Back] -> population_modify

=== population_remove ===
~ ModifyPopulation(-3)
Population decreased by 3!
Current population: {GetPopulation()}

* [Back] -> population_modify

=== housing_check ===
Current housing: {GetHousing()}
Current population: {GetPopulation()}
Occupied housing: {GetPopulation() > GetHousing() ? GetHousing() : GetPopulation()}
Free housing: {GetHousing() - GetPopulation() > 0 ? GetHousing() - GetPopulation() : 0}

* [Back] -> population_test

=== housing_modify ===
* [Add 3 housing] -> housing_add
* [Remove 2 housing] -> housing_remove
* [Back] -> population_test

=== housing_add ===
~ ModifyHousing(3)
Housing increased by 3!
Current housing: {GetHousing()}

* [Back] -> housing_modify

=== housing_remove ===
~ ModifyHousing(-2)
Housing decreased by 2!
Current housing: {GetHousing()}
Note: If this caused housing deficit, excess population was converted to vagrants.

* [Back] -> housing_modify

=== vagrants_check ===
Current vagrants: {GetVagrants()}
Current population: {GetPopulation()}
Total people: {GetVagrants() + GetPopulation()}

* [Back] -> population_test

=== vagrants_modify ===
* [Add 2 vagrants] -> vagrants_add
* [Remove 1 vagrant] -> vagrants_remove
* [Back] -> population_test

=== vagrants_add ===
~ ModifyVagrants(2)
Vagrants increased by 2!
Current vagrants: {GetVagrants()}

* [Back] -> vagrants_modify

=== vagrants_remove ===
~ ModifyVagrants(-1)
Vagrants decreased by 1!
Current vagrants: {GetVagrants()}

* [Back] -> vagrants_modify

=== deaths_check ===
Current deaths: {GetDeaths()}
Current population: {GetPopulation()}
Total people ever: {GetPopulation() + GetDeaths()}

* [Back] -> population_test

=== deaths_process ===
* [Kill 2 people] -> deaths_kill
* [Back] -> population_test

=== deaths_kill ===
~ ProcessEventDeaths(2)
2 people died from this event!
Current population: {GetPopulation()}
Total deaths: {GetDeaths()}

* [Back] -> deaths_process 