# Night 3 · The yard light

The yard is his way to the house. While the light out there is on, he stands still in the dark by the gate, waiting. I know, because I have seen him.

That light has not come on for years. Fix the `YardLight` class:

| Method | What it returns | Example |
| --- | --- | --- |
| `IsDark(lux)` | Whether it is dark outside: less than 50 lux | `IsDark(10)` returns `true` |
| `ShouldLight(lux, hour)` | Whether the light should be on: only in the dark, and never from 5 am to 5 pm | `ShouldLight(10, 22)` returns `true` and `ShouldLight(10, 12)` returns `false` |
| `Label(on)` | The panel's text: `"ON"` with the light on, `"OFF"` with it off | `Label(true)` returns `"ON"` |

He does not come by day, and the battery is short. So from 5 am to 5 pm the light stays off, even on a dark rainy day. From 6 pm on, it comes on in the dark again. The hour goes from 0 to 23.

`IsDark` still works and shows how an `if` decides. Do the other two the same way.
