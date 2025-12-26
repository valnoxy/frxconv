# frxconv

Convert Roaming Profile to FSLogix User Disk.

## 🔧 Usage

´´´
Syntax:
  frxconv.exe [Domain\Username] [Path\To\Store] [Disk Size in MB] (-dynamic)

Options:
  [Domain\Username]       Define the user you want to migrate.

  [Path\To\Store]         File path to the destination of the virtual disk.

  [Disk Size in MB]       Size of the virtual disk in MB.

  -dynamic                Create a dynamic virtual disk.

Example:
  frxconv.exe Contoso\John.Doe D:\FSLogixStore 30720 -dynamic
´´´

## 🧾 License
Frxconv is licensed under [MIT](https://github.com/valnoxy/frxconv/blob/main/LICENSE). So you are allowed to use freely and modify the application. I will not be responsible for any outcome. Proceed with any action at your own risk.

<hr>
<h6 align="center">© 2018 - 2026 valnoxy. All Rights Reserved. 
<br>
By Jonas Günner &lt;jonas@exploitox.de&gt;</h6>
<p align="center">
	<a href="https://github.com/valnoxy/frxconv/blob/main/LICENSE"><img src="https://img.shields.io/static/v1.svg?style=for-the-badge&label=License&message=MIT%20LICENSE&logoColor=d9e0ee&colorA=363a4f&colorB=b7bdf8"/></a>
</p