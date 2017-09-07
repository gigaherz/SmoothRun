# SmoothRun

Automatically launch applications with a (configurable) cooldown per app.

## Usage
* Drop links to your applications (or websites) in `%appdata%\Microsoft\Windows\Start Menu\Programs\Smooth Startup`
** Optionally, also drop them in `%ProgramData%\Microsoft\Windows\Start Menu\Programs\Smooth Startup`
** Add an (optional) config file in any of those folders (named `.smoothconfig`)
* Start SmoothRun

## Example `.smoothconfig`

```
<config>
    <Timeout>20</Timeout>
    <FirstIsSpecial>false</FirstIsSpecial>
</config>
```

## SmoothRun in action

![Example](https://raw.githubusercontent.com/gigaherz/SmoothRun/master/images/starting.gif)

