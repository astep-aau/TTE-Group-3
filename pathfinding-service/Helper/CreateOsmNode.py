#!/usr/bin/env python3
import sys, os, json, time, requests
from dotenv import load_dotenv

# === CONFIGURATION =========================================================
TOKEN_FILE = "osm_token.json"
API_BASE = "https://api.openstreetmap.org/api/0.6"
AUTH_BASE = "https://www.openstreetmap.org/oauth2"

# # Load credentials from .env file (not needed if token is already saved)
# load_dotenv()
# CLIENT_ID = os.getenv("CLIENT_ID")
# CLIENT_SECRET = os.getenv("CLIENT_SECRET")
# 
# if not CLIENT_ID or not CLIENT_SECRET:
#     print("❌ Missing CLIENT_ID or CLIENT_SECRET in .env file.")
#     sys.exit(1)

# === TOKEN MANAGEMENT ======================================================
def save_token(token: dict):
    """Save OAuth token JSON to file."""
    with open(TOKEN_FILE, "w") as f:
        json.dump(token, f)

def load_token():
    """Load token from file if it exists."""
    if os.path.exists(TOKEN_FILE):
        with open(TOKEN_FILE) as f:
            return json.load(f)
    return None

def get_token():
    """Retrieve valid access token (refresh or prompt if needed)."""
    token = load_token()
    if token:
        return token["access_token"]
    else:
        print("No existing token found.")
        return None

    # # Manual login (not necessary if token exists)
    # auth_url = (
    #     f"{AUTH_BASE}/authorize?"
    #     f"client_id={CLIENT_ID}&response_type=code&scope=write_api&"
    #     f"redirect_uri=urn:ietf:wg:oauth:2.0:oob"
    # )
    # print("🌍 Open this URL in your browser to authorize:")
    # print(auth_url)
    # code = input("\nPaste the authorization code here: ").strip()
    # 
    # # Exchange for access token
    # data = {
    #     "grant_type": "authorization_code",
    #     "client_id": CLIENT_ID,
    #     "client_secret": CLIENT_SECRET,
    #     "code": code,
    #     "redirect_uri": "urn:ietf:wg:oauth:2.0:oob",
    # }
    # r = requests.post(f"{AUTH_BASE}/token", data=data)
    # r.raise_for_status()
    # token = r.json()
    # save_token(token)
    # return token["access_token"]

# === MAIN SCRIPT ===========================================================
ACCESS_TOKEN = get_token()
HEADERS = {
    "Authorization": f"Bearer {ACCESS_TOKEN}",
    "Content-Type": "text/xml; charset=utf-8"
}

if len(sys.argv) != 3:
    print("Usage: create_osm_node_oauth.py <lat> <lon>")
    sys.exit(1)

lat, lon = float(sys.argv[1]), float(sys.argv[2])

# --- Step 1: Create a changeset --------------------------------------------
changeset_xml = (
    "<osm><changeset>"
    "<tag k='comment' v='Node created via OAuth script'/>"
    "<tag k='created_by' v='OSM Node Creator Script'/>"
    "</changeset></osm>"
)
r = requests.put(f"{API_BASE}/changeset/create", headers=HEADERS, data=changeset_xml.encode("utf-8"))
try:
    r.raise_for_status()
except requests.exceptions.HTTPError:
    print(f"❌ Failed to create changeset: {r.status_code}\n{r.text}")
    raise
changeset_id = r.text.strip()
print(f"🗺️ Opened changeset {changeset_id}")

# --- Step 2: Create node ---------------------------------------------------
node_xml = (
    f"<osm>"
    f"<node changeset='{changeset_id}' lat='{lat}' lon='{lon}'>"
    f"<tag k='source' v='oauth_script'/>"
    f"</node>"
    f"</osm>"
)
r = requests.put(f"{API_BASE}/node/create", headers=HEADERS, data=node_xml.encode("utf-8"))
try:
    r.raise_for_status()
except requests.exceptions.HTTPError:
    print(f"❌ Node creation failed: {r.status_code}\n{r.text}")
    raise
node_id = r.text.strip()
print(f"✅ Created node ID {node_id} at ({lat}, {lon})")

# --- Step 3: Close changeset ----------------------------------------------
r = requests.put(f"{API_BASE}/changeset/{changeset_id}/close", headers=HEADERS)
try:
    r.raise_for_status()
except requests.exceptions.HTTPError:
    print(f"⚠️ Failed to close changeset {changeset_id}: {r.status_code}\n{r.text}")
    raise
print("📦 Closed changeset successfully.")