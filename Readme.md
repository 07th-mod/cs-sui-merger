The program is configured via a conf.toml file which must be in the same directory as where the program is run from (if you are running via Visual Studio, you can edit the conf.toml in the source directory and it will be used when you run the program)

An example conf.toml is provided in the source folder - it contains many comments, please read it first.

The program takes an input ps3 xml file(specified by `ps3_xml_path` in the toml file), and an input mg script, and outputs to a specified folder `output_folder`

You specify the input mg scripts by adding sections like the following in the toml file:

```
[[input]]
path = "input/tsumi_025_3.txt"
ps3_regions = [ [91816,92563] ]
```

Each input must start with `[[input]]`, contain the path to the file, and the ps3 dialogue id region to match against.

The other parameters are:

- debug - not used
- working_directory - all relative paths will be relative to this folder - the .exe will execute from this folder
- temp_folder - temporary folder (debugging diffs are also put in this folder)
- ps3_merged_output_path - only used when merging multiple .xml files into a single xml file. the single output xml file will be put at this path.
