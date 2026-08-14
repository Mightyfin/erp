p = 'src/Mightyfin.Erp.Hrm.Tests/Mightyfin.Erp.Hrm.Tests.csproj'
s = open(p).read()

# Normalize: remove all PackageReference lines for the three added packages
# and all Api project references; then add exactly one copy of each.
for pkg in ['Microsoft.AspNetCore.Mvc.Testing', 'Microsoft.AspNetCore.TestHost',
            'Microsoft.AspNetCore.Authentication.JwtBearer']:
    s = __import__('re').sub(
        rf'\s*<PackageReference Include="{pkg}"[^>]*/>\s*', '\n    ', s)
s = s.replace('<ProjectReference Include="..\\Mightyfin.Erp.Hrm.Api\\Mightyfin.Erp.Hrm.Api.csproj" />', '')

s = s.replace('</ItemGroup>',
              '<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />\n'
              '    <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="10.0.0" />\n'
              '    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />\n'
              '    <ProjectReference Include="..\\Mightyfin.Erp.Hrm.Api\\Mightyfin.Erp.Hrm.Api.csproj" />\n'
              '  </ItemGroup>', 1)

open(p, 'w').write(s)
print('ok')
