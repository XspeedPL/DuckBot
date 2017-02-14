# DuckBot
![DuckBot Logo](http://i66.tinypic.com/33yhvnr.png)  
DuckBot is a Discord bot with it's own functional-style scripting language (yet to be named).  
With DuckBot users are able to define their own commands using the scripting language mentioned above, Lua or C\# for all possible needs and purposes.  

## Scripting syntax
Text contained within a script is outputted exactly as it is, with the exception of a few special characters.  
Those characters are: `{`, `,` and `|`. The first two are used to execute functions.  
The last is used to construct a Switch-type command. More on that later.  
Special characters can be escaped by using the `^` character.  
Functions share the following syntax: `{func_name:arg_1,arg_2,...}`.  
All functions have a specified minimum amount of arguments, if more than neccessary are passed to the function, they are ignored.  

### Available functions:
* `{command:name,args}` - Executes an user-made command, passing the `args` as text normally passed by the user
* `{date}` - Returns the current date in short format
  * `{date:format}` - Returns the current date and time in specified `format` (ISO 8601)
* `{eval:script}` - Evaluates `script` string as a script by stripping all, if any character escapes
* `{find:str,val}` - Returns the position of first occurence of `val` inside `str` string, or -1 if none were found
* `{if:str1,str2,match,else}` - Compares `str1` with `str2` and returns `match` or `else` based on whether they match or not
* `{input}` - Returns the text passed by the user with the command
  * `{input:n}` - Returns only the `n`-th word of the text
* `{length:str}` - Returns the length of the `str` string
* `{mention}` - Returns a mention to the user who sent the command
  * `{mention:user}` - Returns a mention to a user with specified name
* `{nickOrUser}` - Returns the nickname of the user who sent the command, if the user has no nickname his name is returned instead
* `{rand:max}` - Returns a random number between 0 and `max`-1
  * `{rand:min,max}` - Returns a random number between `min` and `max`-1
* `{substr:str,i}` - Returns a substring of `str` starting at position `i`, or if it's negative position `i` from the end
  * `{substr:str,i,len}` - Returns a substring of `str` starting at `i` with length of `len`, `i` negativity rule applies
* `{time}` - Returns the current time in short format
  * `{time:long}` - Returns the current time in long format
* `{user}` - Returns the name of the user who sent the command

### Special functions:
* `{set:var,value}` - Sets a variable named `var` to `value` in the current server session, returns nothing
  * *Special note: variables with names prefixed by `_` are not persisted between bot runs!*
* `{get:var}` - Returns the contents of variable `var`

## Switch construction
Switches can help compacting a series of `{if}` functions and have a following syntax:  
```
COMPARED_VALUE
| case "value1" script1
| case "value2" script2
| default script3
```  
All values can be scripts too!  

## Lua scripts
Lua scripts run inside a sandboxed environment.  
Inside the environment it's possible to access a few parameters:
* `channel` - The object of the channel the command was sent from
* `rawText` - The text passed by the user with the command
* `sender` - The object of user who sent the command
* `server` - The current server object

## C\# scripts
At the moment only users approved by a superuser (hardcoded as EchoNex) are able to define C# scripts due to unsandboxed access, which could allow malicious code to potentially harm the server.  
Command contents are wrapped inside this template and executed:
```cs
using System; using System.Collections.Generic;
using Discord.Net; using Discord;
namespace DuckCommand {
    public class Command {
        public static string Main(string rawText, User sender, Server server, Channel channel) {
            // Contents inserted here
}   }   }
```  

## Have fun using the bot!
