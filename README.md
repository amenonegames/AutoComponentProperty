# AutoComponentProperty

This is source generator project.
Please use Dll in Release page.

If you writen following code,

```csharp
    public partial class TestClass : MonoBehaviour
    {
        [CompoProp(GetFrom.This)] private Rigidbody _myRigidbody;
        [CompoProp(GetFrom.Children)] private Rigidbody _myRigidbodyInChildren;
        [CompoProp(GetFrom.Parent)] private Rigidbody _myRigidbodyInParent;
    }
```

Then, This code is automatically generated

```csharp
public partial class TestClass
{

    private UnityEngine.Rigidbody MyRigidbody => _myRigidbody is null 
                ? (_myRigidbody = GetComponent<UnityEngine.Rigidbody>())
                : _myRigidbody;
    private UnityEngine.Rigidbody MyRigidbodyInChildren => _myRigidbodyInChildren is null 
                ? (_myRigidbodyInChildren = GetComponentInChildren<UnityEngine.Rigidbody>(true))
                : _myRigidbodyInChildren;
    private UnityEngine.Rigidbody MyRigidbodyInParent => _myRigidbodyInParent is null 
                ? (_myRigidbodyInParent = GetComponentInParent<UnityEngine.Rigidbody>(true))
                : _myRigidbodyInParent;
}
```

you can access ```MyRigidbody``` property in the code.
If original variable is array, the code generated change GetComponent methods to GetComponents methods.
