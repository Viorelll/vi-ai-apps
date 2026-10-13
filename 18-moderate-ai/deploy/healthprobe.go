package main
import("net/http";"os";"time")
func main(){ if len(os.Args)!=2 {os.Exit(2)}; client:=http.Client{Timeout:3*time.Second};r,e:=client.Get(os.Args[1]);if e!=nil{os.Exit(1)};defer r.Body.Close();if r.StatusCode!=200{os.Exit(1)} }
