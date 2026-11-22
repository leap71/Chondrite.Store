#!/usr/bin/env python3
"""Verify the packed runtime with the published compiler in a fresh .NET consumer."""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile


SCHEMA = """chondrite-schema 1
schema Smoke.Pascals
{
    0: fValue float64
}
schema Smoke.Measurement
{
    0: idNonce nonce
    1: uPressure Smoke.Pascals
    2: vecPosition vector3f
    3: astrTags utf8[]
}
"""

PROGRAM = """using System.Collections.Immutable;
using System.Numerics;
using Chondrite.Core;
using Chondrite.Store;
using Chondrite.Store.Serialization;
using Smoke;

Measurement oOriginal = new(
    new Nonce(new Guid("00112233-4455-6677-8899-aabbccddeeff")),
    new Pascals(101325.25), new Vector3(1.25f, -2.5f, 3.75f), ["engineering", "Δ"]);
FixtureStore oStore = new();
StorageObjectId idObject = oStore.idPut(in oOriginal);
Measurement oRestored = oStore.oGet<Measurement>(idObject);
if (oRestored.idNonce != oOriginal.idNonce ||
    oRestored.uPressure.fValue != oOriginal.uPressure.fValue ||
    oRestored.vecPosition != oOriginal.vecPosition ||
    !oRestored.astrTags.SequenceEqual(oOriginal.astrTags))
    throw new InvalidOperationException("The generated record did not round-trip.");
if (oStore.idPut(in oRestored) != idObject || oStore.nCount != 3)
    throw new InvalidOperationException("Record and schema publication was not idempotent.");
StorageObject oSchema = new(StorageType.idIntrinsicSchema,
    ImmutableArray.CreateRange(File.ReadAllBytes(args[0])));
if (oSchema.idCalculateIdentity() != Measurement.idSchema.idSchema)
    throw new InvalidOperationException("Generated identity disagrees with the canonical schema.");
Console.WriteLine("Packed Store + published Compile: generated record round-trip passed.");

namespace Smoke
{
    public readonly partial record struct Pascals
    {
        public Pascals(double fValue)
        {
            m_oData = new SchemaData(fValue);
        }
        public double fValue => m_oData.fValue;
    }

    public readonly partial record struct Measurement
    {
        public Measurement(Nonce idNonce, Pascals uPressure,
            Vector3 vecPosition, ImmutableArray<string> astrTags)
        {
            m_oData = new SchemaData(idNonce, uPressure, vecPosition, astrTags);
        }
        public Nonce idNonce => m_oData.idNonce;
        public Pascals uPressure => m_oData.uPressure;
        public Vector3 vecPosition => m_oData.vecPosition;
        public ImmutableArray<string> astrTags => m_oData.astrTags;
    }

    internal sealed class FixtureStore : IObjectStore
    {
        private readonly Dictionary<StorageObjectId, StorageObject> m_oObjects = [];
        public int nCount => m_oObjects.Count;

        public StorageObjectId idPut(StorageObject oObject, StorageObjectId? idPredecessorHint = null)
        {
            StorageObjectId idObject = oObject.idCalculateIdentity();
            if (oObject.idType != StorageType.idIntrinsicSchema &&
                (!m_oObjects.TryGetValue(oObject.idType.idSchema, out StorageObject? oSchema) ||
                 oSchema.idType != StorageType.idIntrinsicSchema))
                throw new InvalidOperationException("Schema must be published first.");
            if (m_oObjects.TryGetValue(idObject, out StorageObject? oExisting))
            {
                if (oExisting.idType != oObject.idType ||
                    !oExisting.abyData.AsSpan().SequenceEqual(oObject.abyData.AsSpan()))
                    throw new InvalidDataException("Identity collision.");
            }
            else
                m_oObjects.Add(idObject, oObject);
            return idObject;
        }

        public StorageObject? oGet(StorageObjectId idObject) => m_oObjects.GetValueOrDefault(idObject);
        public bool bContains(StorageObjectId idObject) => m_oObjects.ContainsKey(idObject);
    }
}
"""


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def inspect_package(package: Path) -> str:
    with zipfile.ZipFile(package) as archive:
        names = set(archive.namelist())
        manifests = [name for name in names if name.endswith(".nuspec")]
        require(len(manifests) == 1, "Expected one NuGet manifest.")
        manifest = ET.fromstring(archive.read(manifests[0]))
        for element in manifest.iter():
            element.tag = element.tag.rsplit("}", 1)[-1]
        require(manifest.findtext("metadata/id") == "Chondrite.Store", "Wrong package identity.")
        version = manifest.findtext("metadata/version")
        require(bool(version), "Missing package version.")
        required = {"lib/net10.0/Chondrite.Store.dll", "lib/net10.0/Chondrite.Store.xml", "README.md"}
        require(required <= names, f"Missing package files: {sorted(required - names)}")
        require({name for name in names if name.endswith(".dll")} == {"lib/net10.0/Chondrite.Store.dll"},
                "The runtime package must not embed compiler, dependency, or backend assemblies.")
        dependencies = list(manifest.iter("dependency"))
        require(len(dependencies) == 1 and dependencies[0].get("id") == "Chondrite.Core" and
                dependencies[0].get("version") == "0.9.0-alpha02",
                "Expected only the Chondrite.Core 0.9.0-alpha02 runtime dependency.")
        return str(version)


def write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(text.encode("utf-8"))


def run(command: list[str], directory: Path, environment: dict[str, str]) -> str:
    result = subprocess.run(command, cwd=directory, env=environment, text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    require(result.returncode == 0,
            f"Command failed: {subprocess.list2cmdline(command)}\n{result.stdout}")
    return result.stdout


def exercise(package: Path, version: str, dotnet: str, work: Path) -> None:
    environment = os.environ.copy()
    environment.update(DOTNET_ROOT=str(Path(dotnet).parent), DOTNET_CLI_HOME=str(work / "dotnet-home"),
                       DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1", DOTNET_CLI_TELEMETRY_OPTOUT="1",
                       NUGET_PACKAGES=str(work / "packages"))
    environment["PATH"] = str(Path(dotnet).parent) + os.pathsep + environment.get("PATH", "")
    feed = work / "feed"
    feed.mkdir()
    shutil.copy2(package, feed / package.name)
    configuration = ET.Element("configuration")
    sources = ET.SubElement(configuration, "packageSources")
    ET.SubElement(sources, "clear")
    ET.SubElement(sources, "add", key="local", value=str(feed))
    ET.SubElement(sources, "add", key="nuget.org", value="https://api.nuget.org/v3/index.json")
    mappings = ET.SubElement(configuration, "packageSourceMapping")
    ET.SubElement(ET.SubElement(mappings, "packageSource", key="local"), "package", pattern="Chondrite.Store")
    ET.SubElement(ET.SubElement(mappings, "packageSource", key="nuget.org"), "package", pattern="*")
    ET.ElementTree(configuration).write(work / "NuGet.Config", encoding="utf-8", xml_declaration=True)
    consumer = work / "consumer with spaces"
    write(consumer / "Consumer.csproj", f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Chondrite.Store" Version="{version}" />
    <PackageReference Include="Chondrite.Compile" Version="0.9.0-alpha01" PrivateAssets="all" />
  </ItemGroup>
</Project>
""")
    write(consumer / "Schemas" / "Measurement.chondrite", SCHEMA)
    write(consumer / "Program.cs", PROGRAM)
    run([dotnet, "build", "Consumer.csproj", "--configuration", "Release", "--nologo", "-m:1", "-nr:false"],
        consumer, environment)
    generated = consumer / "obj" / "Release" / "net10.0" / "ChondriteSchemas"
    require({path.name for path in generated.glob("*.g.cs")} == {"Smoke.Pascals.g.cs", "Smoke.Measurement.g.cs"},
            "The published compiler did not generate both records during the build.")
    output = consumer / "bin" / "Release" / "net10.0"
    require(not (output / "Chondrite.Compile.dll").exists(), "The compiler leaked into the application output.")
    print(run([dotnet, str(output / "Consumer.dll"), str(generated / "Smoke.Measurement.canonical.chondrite")],
              consumer, environment).strip())


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, required=True)
    parser.add_argument("--dotnet", default="dotnet")
    arguments = parser.parse_args()
    package = arguments.package.resolve()
    dotnet = shutil.which(arguments.dotnet)
    require(dotnet is not None, f"Cannot find .NET SDK host: {arguments.dotnet}")
    version = inspect_package(package)
    print(f"Checking Chondrite.Store {version} with Chondrite.Compile 0.9.0-alpha01", flush=True)
    with tempfile.TemporaryDirectory(prefix="chondrite-store-smoke-") as directory:
        exercise(package, version, str(Path(dotnet).resolve()), Path(directory))


if __name__ == "__main__":
    main()
