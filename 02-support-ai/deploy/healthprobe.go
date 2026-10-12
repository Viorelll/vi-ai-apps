package main
import("net/http";"os";"time")
func main(){client:=http.Client{Timeout:3*time.Second};r,e:=client.Get("http://127.0.0.1:9000/minio/health/live");if e!=nil{os.Exit(1)};defer r.Body.Close();if r.StatusCode!=200{os.Exit(1)}}
