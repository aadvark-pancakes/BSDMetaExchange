# OasisMetaExchange

Small .NET App that simulates a small meta exchange, thatgives the user the best possible price when buying or selling a certain amount of BTC.

## Solution Design

The solution is divided into three project
- Core: contains the main logic of the app. For simplicity's sake, it contains what usually would be in domain, infrastructure, I/O, persistance, etc.
- Console: a simple console app for the user to get the best recommendations
- API: a simple API with several endpoints for basic communication

The brunt of the processing is in **MetaExchangeEngine**. 

To keep it simple and not overengineer, the following assumptions were made:

- no follow up questions were sent to define req's / clear up questions after initial receiving of task, as would happen in real life
- the json files were assumed to be valid, therefore only basic validation was done - if one file parsing failed, the processing goes onto the next one, or checking if amount / price are above 0.
- InMemoryExchangeRepository exists only as an example to show decoupling, and so is almost empty. It serves so that it can be easily exchanged for a real repository class.
- Since the provided json files were relatively small, performance was not taken into account when choosing how to store the orders in memory (List was used)
- Only basic validation was done at all steps, as an example
- Only unit tests were made for the Core/Services services. No integration, end to end or similar tests were made
- Minimum order size was not implemented, therefore the user can theoretically buy / sell unrealistic amounts. Same goes for rounding, dust rules, etc.
- the json files themselves were used for the unit tests, instead of mocks, to simulate testing out on (near) real world values.

AI was used in the following:
- to create boiler plate code
- to create models
- to add comments & logs
- for setting up unit tests
- for creating & updating of some unit tests
