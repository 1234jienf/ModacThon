using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;

public class LoginManager : MonoBehaviour
{
    public Button btnClose;
    public GameObject LoginView;
    public TMP_InputField inputField_ID;
    public TMP_InputField inputField_PW;
    public TMP_Text check;
    public Button Button_Login;
    public Button GoMain;
    public Button DoLogin;
    public enum APIType
    {
        Login,
        Logout,
    }
    private void Start() {
        LoginView.SetActive(false);
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            // 연결이 안된경우
            ErrorCheck(-1000);
        }
    }
    public void DoLoginView()
    {
        LoginView.SetActive(true);
        inputField_ID.Select();
    }

    public void CloseLoginView()
    {
        check.text = "";
        inputField_ID.text = "";
        inputField_PW.text = "";
        LoginView.SetActive(false);
    }

    public void LoginButtonClick()
    {
        StartCoroutine(APIExample(APIType.Login));
    }

    IEnumerator APIExample(APIType _type)
    {
        switch (_type)
        {
            case APIType.Login:
                yield return StartCoroutine(API_Login());
                break;
        }
        yield return null;
    }

    public class LoginData {
        public string id;
        public string password;
    }

    [System.Serializable]
    public class UserData
    {
        public UserDataDetails data;
        public TokenDetails token;
    }
    [System.Serializable]
    public class Food
    {
        public string code;
        public int count;
    }

    [System.Serializable]
    public class Inventory
    {
        public string code;
        public int count;
        public int idx;
    }

    [System.Serializable]
    public class UserDataDetails
    {
        public string id;
        public string username;
        public string password;
        public string role;
        public int lv;
        public int exp;
        public int money;
        public int hp;
        public int currentArm;
        public int currentVehicle;
        public double time;
        public string[] learnedList;
        public Food[] todayMenus;
        public int workers;
        public int onDutyWorkers;
        public Food[] soldFood;
        public bool noSound;
        public int questSuccess;
        public string lastScene;
        public double[] lastXy;
        public Inventory[] inventory;
        public string currentField;

    }
    [System.Serializable]
    public class TokenDetails
    {
        public string accessToken;
        public string refreshToken;
    }
    // API로 로그인하여 토큰을 가져오는 함수
    IEnumerator API_Login()
    {
        LoginData loginData = new LoginData();
        loginData.id = inputField_ID.text;
        loginData.password = inputField_PW.text;
        Debug.Log(loginData.id);
        Debug.Log(loginData.password);
        string json = JsonUtility.ToJson(loginData);
        byte[] myData = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest www = new UnityWebRequest("https://j10a609.p.ssafy.io/api/auth/login", "POST");
        UploadHandlerRaw uhr = new UploadHandlerRaw(myData);
        uhr.contentType = "application/json";
        www.uploadHandler = uhr;
        www.downloadHandler = new DownloadHandlerBuffer();
        yield return www.SendWebRequest();
        Debug.Log(www.result);
        if (www.result == UnityWebRequest.Result.Success)
        {
            check.text = "";
            UserData userData = JsonUtility.FromJson<UserData>(www.downloadHandler.text);
            LoginView.SetActive(false);
            Debug.Log(www.downloadHandler.text);
            Debug.Log(userData.data.soldFood);
            GameMgr.Instance.id = userData.data.id;
            GameMgr.Instance.username = userData.data.username;
            GameMgr.Instance.password = userData.data.password;
            GameMgr.Instance.role = userData.data.role;
            GameMgr.Instance.lv = userData.data.lv;
            GameMgr.Instance.exp = userData.data.exp;
            GameMgr.Instance.money = userData.data.money;
            GameMgr.Instance.hp = userData.data.hp;
            GameMgr.Instance.currentArm = userData.data.currentArm;
            GameMgr.Instance.currentVehicle = userData.data.currentVehicle;
            GameMgr.Instance.time = (float)userData.data.time;
            for (int i = 0; i < userData.data.learnedList.Length; i++){
                GameMgr.Instance.learnedList.Add(userData.data.learnedList[i]);
            }
            for (int i = 0; i < userData.data.todayMenus.Length; i++){
                GameMgr.Instance.todayMenus.Add(userData.data.todayMenus[i]);
            }
            for (int i = 0; i < userData.data.soldFood.Length; i++){
                GameMgr.Instance.soldFood.Add(userData.data.soldFood[i]);
            }
            for (int i = 0; i < userData.data.inventory.Length; i++){
                GameMgr.Instance.inventory.Add(userData.data.inventory[i]);
            }
            GameMgr.Instance.workers = userData.data.workers;
            GameMgr.Instance.onDutyWorkers = userData.data.onDutyWorkers;
            GameMgr.Instance.noSound = userData.data.noSound;
            GameMgr.Instance.questSuccess = userData.data.questSuccess;
            GameMgr.Instance.lastScene = userData.data.lastScene;
            GameMgr.Instance.lastXy = userData.data.lastXy;
            GameMgr.Instance.currentField = userData.data.currentField;

            GameMgr.Instance.LoadInventory();
            GameMgr.Instance.accessToken = userData.token.accessToken;
            if (GameMgr.Instance.lv == 1 && GameMgr.Instance.exp == 0 && GameMgr.Instance.money == 0)
            {
                // Tutorial Story 씬으로 이동
                SceneManager.LoadScene("Tutorial Story");
            }
            else
            {
                // 그렇지 않으면 Main 씬으로 이동
                SceneManager.LoadScene("Main");
            }
        }
        else
        {
            check.text = "아이디 또는 비밀번호를 확인해주세요.";
        }
        
    }

    int ErrorCheck(int _code)
    {
        if (_code > 0) return 0;
        else if (_code == -1000)
            Debug.LogError(_code + ", Internet Connect Error");
        else if (_code == -1001)
            Debug.LogError(_code + ", Occur token type Error");
        else if (_code == -1002)
            Debug.LogError(_code + ", Category type Error");
        else if (_code == -1003)
            Debug.LogError(_code + ", Item type Error");
        else
            Debug.LogError(_code + ", Undefined Error");
        return _code;
    }
    int ErrorCheck(int _code, string _funcName)
    {
        if (_code > 0) return 0;
        else if (_code == -400) Debug.LogError(_code + ", Invalid request in " + _funcName);
        else if (_code == -401) Debug.LogError(_code + ", Unauthorized in " + _funcName);
        else if (_code == -404) Debug.LogError(_code + ", not found in " + _funcName);
        else if (_code == -500) Debug.LogError(_code + ", Internal Server Error in " + _funcName);
        else Debug.LogError(_code + ", Undefined Error");
        return _code;
    }
    private void Update() {
        if (inputField_ID.isFocused){
            if (Input.GetKeyDown(KeyCode.Tab)){
                inputField_PW.Select();
            }
        }
        if (inputField_PW.isFocused){
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Tab)){
                inputField_ID.Select();
            }
        }
        if (Input.GetKeyDown(KeyCode.Return)){
            LoginButtonClick();
        }
    }

    public void Close()
    {
    }
}
