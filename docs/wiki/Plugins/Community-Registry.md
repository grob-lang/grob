# Community Registry

Any C# class library implementing `IGrobPlugin` can be published as a community
plugin. Published to NuGet tagged `grob-plugin`.

```grob
import AcmeCorp.Xml           // alias: xml.*
import AcmeCorp.Xml as parser // explicit alias
```

## Listing Requirements

Inclusion in the `PLUGINS.md` registry requires: a public repo, a README, and
a licence. Listing is not a safety endorsement.

**Warning:** Loading a plugin is equivalent to running arbitrary code. Quality
and security are the author's responsibility.
