     public static async Task TranscribeConversationsAsync(string voiceSignatureStringUser1, string voiceSignatureStringUser2, string filepath)
        {
            var config = SpeechConfig.FromSubscription(SubscriptionKey, Region);
            config.SpeechRecognitionLanguage = "en-us";
         
            config.SetProperty("ConversationTranscriptionInRoomAndOnline", "true");
            var stopRecognition = new TaskCompletionSource<int>();
         
            //var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            Stopwatch totalDurationWatch = new Stopwatch();
         
            using (var audioInput = AudioStreamReader.OpenWavFile(filepath))
            {
                var meetingID = Guid.NewGuid().ToString();
                Console.WriteLine($"MeetingID: {meetingID}");
                        
                using (var conversation = await Conversation.CreateConversationAsync(config, meetingID))
                {
                    // create a conversation transcriber using audio stream input
                    using (var conversationTranscriber = new ConversationTranscriber(audioInput))
                    {
                        conversationTranscriber.Transcribed += (s, e) =>
                        {
                            string timeStamp = DateTime.Now.ToString("G");
                            if (e.Result.Reason == ResultReason.RecognizedSpeech)
                            {
                                Console.WriteLine($"{timeStamp}\tDuration (in Sec): {e.Result.Duration.TotalSeconds} Offset (in ticks): {e.Result.OffsetInTicks} TRANSCRIBED: Text={e.Result.Text} SpeakerId={e.Result.UserId}");
                            }
                            else if (e.Result.Reason == ResultReason.NoMatch)
                            {
                                Console.WriteLine($"{timeStamp}\tNOMATCH: Speech could not be recognized.");
                            }
                        };
         
                        conversationTranscriber.Canceled += (s, e) =>
                        {
                            string timeStamp = DateTime.Now.ToString("G");
                            Console.WriteLine($"{timeStamp}\tCANCELED: Reason={e.Reason}");
         
                            if (e.Reason == CancellationReason.Error)
                            {
                                Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
                                Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                                Console.WriteLine($"CANCELED: Did you update the subscription info?");
                                stopRecognition.TrySetResult(0);
                            }
                        };
         
                        conversationTranscriber.SessionStarted += (s, e) =>
                        {
                            string timeStamp = DateTime.Now.ToString("G");
                            Console.WriteLine($"\n{timeStamp}\tSession started event. SessionId={e.SessionId}");
                            totalDurationWatch.Start();
                        };
         
                        conversationTranscriber.SessionStopped += (s, e) =>
                        {
                            string timeStamp = DateTime.Now.ToString("G");
                            Console.WriteLine($"{timeStamp}\tSession stopped event. SessionId={e.SessionId}");
                            Console.WriteLine("\nStop recognition.");
                                    
                            Console.WriteLine($"Total duration in seconds: {totalDurationWatch.Elapsed.TotalSeconds}");
                                    
                            totalDurationWatch.Stop();
                            stopRecognition.TrySetResult(0);
                        };
         
                        // Add participants to the conversation.
                        var speaker1 = Participant.From("Pratyush", "en-us", voiceSignatureStringUser1);
                        var speaker2 = Participant.From("Cody Rigsby", "en-us", voiceSignatureStringUser2);
                        await conversation.AddParticipantAsync(speaker1);
                        await conversation.AddParticipantAsync(speaker2);
         
                        //Join to the conversation and start transcribing
                        await conversationTranscriber.JoinConversationAsync(conversation);
         
                        // keeping these lines here for single channel audio 
                        var connection = Connection.FromRecognizer(conversationTranscriber);
                        connection.SetMessageProperty("speech.config", "DisableReferenceChannel", $"\"True\"");
                        connection.SetMessageProperty("speech.config", "MicSpec", $"\"1_0_0\""); 
                        // end
         
                        await conversationTranscriber.StartTranscribingAsync().ConfigureAwait(false);
         
                        // waits for completion, then stop transcription
                        Task.WaitAny(new[] { stopRecognition.Task });
                        await conversationTranscriber.StopTranscribingAsync().ConfigureAwait(false);
                    }
                }
            }
        }
